using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.Songs;
using WeddingOrchestrator.Api.DTOs.Weddings;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class WeddingService : IWeddingService
{
    private readonly AppDbContext _db;
    private readonly IConflictDetectionService _conflicts;

    public WeddingService(AppDbContext db, IConflictDetectionService conflicts)
    {
        _db = db;
        _conflicts = conflicts;
    }

    public async Task<List<WeddingListItemDto>> GetAllAsync()
    {
        var weddings = await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .OrderByDescending(w => w.DateOfWedding)
            .ToListAsync();

        return weddings.Select(MapToListItemDto).ToList();
    }

    public async Task<List<WeddingListItemDto>> CheckAvailabilityAsync(DateTime date, TimeOnly? startTime, TimeOnly? endTime)
    {
        var startOfDay = date.Date;
        var endOfDay   = startOfDay.AddDays(1);

        var weddings = await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .Where(w => w.DateOfWedding >= startOfDay && w.DateOfWedding < endOfDay)
            .ToListAsync();

        if (startTime.HasValue && endTime.HasValue)
        {
            weddings = weddings
                .Where(w => w.StartTime == null || w.EndTime == null ||
                            (startTime.Value < w.EndTime.Value && endTime.Value > w.StartTime.Value))
                .ToList();
        }

        return weddings.Select(MapToListItemDto).ToList();
    }

    private static WeddingListItemDto MapToListItemDto(Wedding w)
    {
        var groom = w.Roles.FirstOrDefault(r => r.RoleType == RoleType.Groom);
        var bride = w.Roles.FirstOrDefault(r => r.RoleType == RoleType.Bride);
        return new WeddingListItemDto
        {
            Id = w.Id,
            Title = WeddingTitleHelper.Compute(w),
            DateOfWedding = w.DateOfWedding,
            StartTime = w.StartTime,
            EndTime = w.EndTime,
            Location = w.Location,
            IsFinalized = w.IsFinalized,
            GroomName = groom?.Person?.FullName ?? string.Empty,
            BrideName = bride?.Person?.FullName ?? string.Empty
        };
    }

    public async Task<WeddingDto> GetByIdAsync(int id)
    {
        var conflictReport = await _conflicts.GetConflictReportAsync(id);
        return await BuildWeddingDtoAsync(id, conflictReport);
    }

    public async Task<WeddingDto> CreateAsync(CreateWeddingDto dto)
    {
        var wedding = new Wedding
        {
            DateOfWedding = dto.DateOfWedding,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Location = dto.Location,
            IsFinalized = false,
            CreatedUtc = DateTime.UtcNow
        };
        _db.Weddings.Add(wedding);
        await _db.SaveChangesAsync();

        int? groomPersonId = null;
        int? bridePersonId = null;

        if (!string.IsNullOrWhiteSpace(dto.GroomName))
            groomPersonId = await FindOrCreatePersonByNameAsync(dto.GroomName);

        if (!string.IsNullOrWhiteSpace(dto.BrideName))
            bridePersonId = await FindOrCreatePersonByNameAsync(dto.BrideName);

        var roles = RoleHelper.AllRoles.Select(roleType => new WeddingRole
        {
            WeddingId = wedding.Id,
            RoleType = roleType,
            PersonId = roleType == RoleType.Groom ? groomPersonId
                     : roleType == RoleType.Bride ? bridePersonId
                     : null
        }).ToList();

        _db.WeddingRoles.AddRange(roles);

        if (dto.WeddingIntroSongId.HasValue)
        {
            var introRole = roles.First(r => r.RoleType == RoleType.WeddingItself);
            introRole.SongAssignments.Add(new WeddingRoleSongAssignment
            {
                SongId = dto.WeddingIntroSongId.Value,
                AssignmentSlot = 1
            });
        }

        await _db.SaveChangesAsync();

        return await GetByIdAsync(wedding.Id);
    }

    public async Task<WeddingDto> UpdateRolesAsync(int id, WeddingFamilyTreeDto dto)
    {
        var (wedding, _) = await LoadFullWedding(id);

        foreach (var slotInput in dto.Roles)
        {
            var role = wedding.Roles.FirstOrDefault(r => r.RoleType == slotInput.RoleType);
            if (role == null) continue;

            var oldPersonId = role.PersonId;

            if (slotInput.PersonId.HasValue)
                role.PersonId = slotInput.PersonId;
            else if (!string.IsNullOrWhiteSpace(slotInput.FreeTextName))
                role.PersonId = await FindOrCreatePersonByNameAsync(slotInput.FreeTextName);
            else
                role.PersonId = null;

            // SongAssignments are already loaded by LoadFullWedding
            if (role.PersonId != oldPersonId)
                _db.WeddingRoleSongAssignments.RemoveRange(role.SongAssignments);
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<WeddingDto> AssignSongsAsync(int id, AssignSongsDto dto)
    {
        var (wedding, _) = await LoadFullWedding(id);

        var conflictReport = await _conflicts.GetConflictReportAsync(id);
        var forbiddenIds = conflictReport.ForbiddenSongIds.ToHashSet();

        foreach (var input in dto.Assignments)
        {
            if (forbiddenIds.Contains(input.SongId))
                throw new DomainException($"Song {input.SongId} is forbidden for this wedding.");

            var role = wedding.Roles.FirstOrDefault(r => r.Id == input.WeddingRoleId)
                ?? throw new KeyNotFoundException($"WeddingRole {input.WeddingRoleId} not found.");

            // SongAssignments already loaded — no extra DB query
            var existing = role.SongAssignments.FirstOrDefault(a => a.AssignmentSlot == input.AssignmentSlot);
            if (existing != null)
                existing.SongId = input.SongId;
            else
                role.SongAssignments.Add(new WeddingRoleSongAssignment
                {
                    SongId = input.SongId,
                    AssignmentSlot = input.AssignmentSlot
                });
        }

        await _db.SaveChangesAsync();
        return await BuildWeddingDtoAsync(id, conflictReport);
    }

    public async Task<WeddingDto> FinalizeAsync(int id)
    {
        var wedding = await _db.Weddings.FindAsync(id)
            ?? throw new KeyNotFoundException($"Wedding {id} not found.");
        wedding.IsFinalized = true;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<WeddingDto> UnfinalizeAsync(int id)
    {
        var wedding = await _db.Weddings.FindAsync(id)
            ?? throw new KeyNotFoundException($"Wedding {id} not found.");
        wedding.IsFinalized = false;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var wedding = await _db.Weddings.FindAsync(id)
            ?? throw new KeyNotFoundException($"Wedding {id} not found.");
        _db.Weddings.Remove(wedding);
        await _db.SaveChangesAsync();
    }

    public async Task<(string roleLabel, string filePath, string songTitle)?> GetRoleSongExportDataAsync(int weddingId, RoleType roleType)
    {
        var role = await _db.WeddingRoles
            .Include(r => r.SongAssignments).ThenInclude(a => a.Song)
            .FirstOrDefaultAsync(r => r.WeddingId == weddingId && r.RoleType == roleType);

        if (role == null) return null;

        var assignment = role.SongAssignments.FirstOrDefault(a => a.AssignmentSlot == 1);
        if (assignment == null) return null;

        return (RoleHelper.GetLabel(roleType), assignment.Song.RelativeFilePath, assignment.Song.Title);
    }

    public async Task<List<(string roleLabel, string songTitle, string filePath)>> GetCombinedExportDataAsync(int weddingId)
    {
        var exists = await _db.Weddings.AnyAsync(w => w.Id == weddingId);
        if (!exists) throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var roles = await _db.WeddingRoles
            .Where(r => r.WeddingId == weddingId)
            .Include(r => r.SongAssignments).ThenInclude(a => a.Song)
            .OrderBy(r => r.RoleType)
            .ToListAsync();

        return roles
            .SelectMany(r => r.SongAssignments
                .OrderBy(a => a.AssignmentSlot)
                .Select(a => (RoleHelper.GetLabel(r.RoleType), a.Song.Title, a.Song.RelativeFilePath)))
            .ToList();
    }

    private async Task<WeddingDto> BuildWeddingDtoAsync(int id, ConflictReportDto conflictReport)
    {
        var (wedding, songsByCategory) = await LoadFullWedding(id);
        return MapWeddingDto(wedding, conflictReport, songsByCategory);
    }

    private async Task<int> FindOrCreatePersonByNameAsync(string name)
    {
        var trimmed = name.Trim();
        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = parts.Length > 0 ? parts[0] : trimmed;
        var lastName = parts.Length > 1 ? parts[1] : string.Empty;

        var existing = await _db.People
            .FirstOrDefaultAsync(p => p.FirstName == firstName && p.LastName == lastName);
        if (existing != null) return existing.Id;

        try
        {
            var person = new Person { FirstName = firstName, LastName = lastName };
            _db.People.Add(person);
            await _db.SaveChangesAsync();
            return person.Id;
        }
        catch (DbUpdateException)
        {
            // Race condition: a concurrent request inserted the same person
            _db.ChangeTracker.Clear();
            return await _db.People
                .Where(p => p.FirstName == firstName && p.LastName == lastName)
                .Select(p => p.Id)
                .FirstAsync();
        }
    }

    private async Task<(Wedding, Dictionary<int, List<Song>>)> LoadFullWedding(int id)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .Include(w => w.Roles).ThenInclude(r => r.SongAssignments).ThenInclude(a => a.Song).ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(w => w.Id == id)
            ?? throw new KeyNotFoundException($"Wedding {id} not found.");

        var songs = await _db.Songs
            .Include(s => s.Category)
            .OrderBy(s => s.Category.DisplayOrder)
            .ThenBy(s => s.Title)
            .ToListAsync();

        var songsByCategory = songs
            .GroupBy(s => s.SongCategoryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return (wedding, songsByCategory);
    }

    private static WeddingDto MapWeddingDto(Wedding w, ConflictReportDto? conflictReport, Dictionary<int, List<Song>> songsByCategory)
    {
        return new WeddingDto
        {
            Id = w.Id,
            Title = WeddingTitleHelper.Compute(w),
            DateOfWedding = w.DateOfWedding,
            StartTime = w.StartTime,
            EndTime = w.EndTime,
            Location = w.Location,
            IsFinalized = w.IsFinalized,
            Roles = w.Roles.OrderBy(r => r.RoleType).Select(r => MapRoleDto(r, conflictReport, songsByCategory)).ToList(),
            ConflictReport = conflictReport
        };
    }

    private static WeddingRoleDto MapRoleDto(WeddingRole role, ConflictReportDto? conflictReport, Dictionary<int, List<Song>> songsByCategory)
    {
        var forbiddenIds = conflictReport?.ForbiddenSongIds.ToHashSet() ?? new HashSet<int>();
        var primaryCategoryIds = RoleHelper.GetRequiredSlots(role.RoleType)
            .Select(s => s.categoryId)
            .ToHashSet();

        var availableSongs = songsByCategory.Values
            .SelectMany(songs => songs.Select(s => new AvailableSongDto
            {
                SongId = s.Id,
                Title = s.Title,
                CategoryId = s.SongCategoryId,
                CategoryName = s.Category?.Name ?? string.Empty,
                AssignmentSlot = 1,
                IsForbidden = forbiddenIds.Contains(s.Id),
                IsPrimaryCategory = primaryCategoryIds.Contains(s.SongCategoryId)
            }))
            .OrderBy(s => s.IsPrimaryCategory ? 0 : 1)
            .ThenBy(s => s.CategoryName)
            .ThenBy(s => s.Title)
            .ToList();

        return new WeddingRoleDto
        {
            Id = role.Id,
            RoleType = role.RoleType,
            RoleLabel = RoleHelper.GetLabel(role.RoleType),
            PersonId = role.PersonId,
            PersonName = role.Person?.FullName,
            SongAssignments = role.SongAssignments.Select(a => new SongAssignmentDto
            {
                AssignmentSlot = a.AssignmentSlot,
                SongId = a.SongId,
                SongTitle = a.Song.Title,
                SongCategoryName = a.Song.Category.Name,
                FileSizeBytes = a.Song.FileSizeBytes
            }).ToList(),
            AvailableSongs = availableSongs
        };
    }
}
