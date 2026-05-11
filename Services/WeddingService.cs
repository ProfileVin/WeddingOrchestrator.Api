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

        return weddings.Select(w =>
        {
            var groom = w.Roles.FirstOrDefault(r => r.RoleType == RoleType.Groom);
            var bride = w.Roles.FirstOrDefault(r => r.RoleType == RoleType.Bride);
            return new WeddingListItemDto
            {
                Id = w.Id,
                Title = WeddingTitleHelper.Compute(w),
                DateOfWedding = w.DateOfWedding,
                Location = w.Location,
                IsFinalized = w.IsFinalized,
                GroomName = groom?.Person?.FullName ?? groom?.FreeTextName ?? string.Empty,
                BrideName = bride?.Person?.FullName ?? bride?.FreeTextName ?? string.Empty
            };
        }).ToList();
    }

    public async Task<WeddingDto> GetByIdAsync(int id)
    {
        var wedding = await LoadFullWedding(id);
        var conflictReport = await _conflicts.GetConflictReportAsync(id);
        return MapWeddingDto(wedding, conflictReport);
    }

    public async Task<WeddingDto> CreateAsync(CreateWeddingDto dto)
    {
        var wedding = new Wedding
        {
            DateOfWedding = dto.DateOfWedding,
            Location = dto.Location,
            IsFinalized = false,
            CreatedUtc = DateTime.UtcNow
        };
        _db.Weddings.Add(wedding);
        await _db.SaveChangesAsync();

        // Pre-create all 15 role slots
        var roles = RoleHelper.AllRoles.Select(roleType => new WeddingRole
        {
            WeddingId = wedding.Id,
            RoleType = roleType
        }).ToList();

        _db.WeddingRoles.AddRange(roles);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(dto.GroomName))
            roles.First(r => r.RoleType == RoleType.Groom).FreeTextName = dto.GroomName;

        if (!string.IsNullOrWhiteSpace(dto.BrideName))
            roles.First(r => r.RoleType == RoleType.Bride).FreeTextName = dto.BrideName;

        if (dto.WeddingIntroSongId.HasValue)
            _db.WeddingRoleSongAssignments.Add(new WeddingRoleSongAssignment
            {
                WeddingRoleId = roles.First(r => r.RoleType == RoleType.WeddingItself).Id,
                SongId = dto.WeddingIntroSongId.Value,
                AssignmentSlot = 1
            });

        await _db.SaveChangesAsync();

        return await GetByIdAsync(wedding.Id);
    }

    public async Task<WeddingDto> UpdateRolesAsync(int id, WeddingFamilyTreeDto dto)
    {
        var wedding = await LoadFullWedding(id);
        if (wedding.IsFinalized)
            throw new InvalidOperationException("Finalized weddings cannot be edited.");

        foreach (var slotInput in dto.Roles)
        {
            var role = wedding.Roles.FirstOrDefault(r => r.RoleType == slotInput.RoleType);
            if (role == null) continue;

            role.PersonId = slotInput.PersonId;
            role.FreeTextName = slotInput.FreeTextName;

            // Clear existing song assignments when the person changes
            var existingAssignments = await _db.WeddingRoleSongAssignments
                .Where(a => a.WeddingRoleId == role.Id)
                .ToListAsync();
            _db.WeddingRoleSongAssignments.RemoveRange(existingAssignments);
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<WeddingDto> AssignSongsAsync(int id, AssignSongsDto dto)
    {
        var wedding = await LoadFullWedding(id);
        if (wedding.IsFinalized)
            throw new InvalidOperationException("Finalized weddings cannot be edited.");

        var forbiddenIds = await _conflicts.GetForbiddenSongIdsAsync(id);

        foreach (var input in dto.Assignments)
        {
            if (forbiddenIds.Contains(input.SongId))
                throw new InvalidOperationException($"Song {input.SongId} is forbidden for this wedding.");

            var role = wedding.Roles.FirstOrDefault(r => r.Id == input.WeddingRoleId)
                ?? throw new KeyNotFoundException($"WeddingRole {input.WeddingRoleId} not found.");

            var existing = await _db.WeddingRoleSongAssignments
                .FirstOrDefaultAsync(a => a.WeddingRoleId == input.WeddingRoleId && a.AssignmentSlot == input.AssignmentSlot);

            if (existing != null)
                existing.SongId = input.SongId;
            else
                _db.WeddingRoleSongAssignments.Add(new WeddingRoleSongAssignment
                {
                    WeddingRoleId = input.WeddingRoleId,
                    SongId = input.SongId,
                    AssignmentSlot = input.AssignmentSlot
                });
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<WeddingDto> FinalizeAsync(int id)
    {
        var wedding = await _db.Weddings.FindAsync(id)
            ?? throw new KeyNotFoundException($"Wedding {id} not found.");
        wedding.IsFinalized = true;
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

    private async Task<Wedding> LoadFullWedding(int id)
    {
        return await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .Include(w => w.Roles).ThenInclude(r => r.SongAssignments).ThenInclude(a => a.Song).ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(w => w.Id == id)
            ?? throw new KeyNotFoundException($"Wedding {id} not found.");
    }

    private WeddingDto MapWeddingDto(Wedding w, ConflictReportDto? conflictReport)
    {
        return new WeddingDto
        {
            Id = w.Id,
            Title = WeddingTitleHelper.Compute(w),
            DateOfWedding = w.DateOfWedding,
            Location = w.Location,
            IsFinalized = w.IsFinalized,
            Roles = w.Roles.OrderBy(r => r.RoleType).Select(r => MapRoleDto(r, conflictReport)).ToList(),
            ConflictReport = conflictReport
        };
    }

    private WeddingRoleDto MapRoleDto(WeddingRole role, ConflictReportDto? conflictReport)
    {
        var forbiddenIds = conflictReport?.ForbiddenSongIds.ToHashSet() ?? new HashSet<int>();
        var requiredSlots = RoleHelper.GetRequiredSlots(role.RoleType);

        // Build available songs per slot
        var availableSongs = new List<AvailableSongDto>();
        foreach (var (slot, categoryId) in requiredSlots)
        {
            var songs = _db.Songs
                .Where(s => s.SongCategoryId == categoryId)
                .Select(s => new AvailableSongDto
                {
                    SongId = s.Id,
                    Title = s.Title,
                    CategoryId = s.SongCategoryId,
                    AssignmentSlot = slot,
                    IsForbidden = forbiddenIds.Contains(s.Id)
                })
                .OrderBy(s => s.Title)
                .ToList();
            availableSongs.AddRange(songs);
        }

        return new WeddingRoleDto
        {
            Id = role.Id,
            RoleType = role.RoleType,
            RoleLabel = RoleHelper.GetLabel(role.RoleType),
            PersonId = role.PersonId,
            PersonName = role.Person?.FullName,
            FreeTextName = role.FreeTextName,
            SongAssignments = role.SongAssignments.Select(a => new SongAssignmentDto
            {
                AssignmentSlot = a.AssignmentSlot,
                SongId = a.SongId,
                SongTitle = a.Song.Title,
                SongCategoryName = a.Song.Category.Name
            }).ToList(),
            AvailableSongs = availableSongs
        };
    }
}
