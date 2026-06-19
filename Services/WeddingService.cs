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
            CreatedUtc = DateTime.Now,
            UpdatedDate = DateTime.Now
        };
        _db.Weddings.Add(wedding);
        await _db.SaveChangesAsync();

        int? groomPersonId = null;
        int? bridePersonId = null;

        if (!string.IsNullOrWhiteSpace(dto.GroomFirstName) || !string.IsNullOrWhiteSpace(dto.GroomLastName))
            groomPersonId = await FindOrCreatePersonByFirstLastAsync(dto.GroomFirstName?.Trim() ?? string.Empty, dto.GroomLastName?.Trim() ?? string.Empty, Gender.Male);
        else if (!string.IsNullOrWhiteSpace(dto.GroomName))
            groomPersonId = await FindOrCreatePersonByNameAsync(dto.GroomName, Gender.Male);

        if (!string.IsNullOrWhiteSpace(dto.BrideFirstName) || !string.IsNullOrWhiteSpace(dto.BrideLastName))
            bridePersonId = await FindOrCreatePersonByFirstLastAsync(dto.BrideFirstName?.Trim() ?? string.Empty, dto.BrideLastName?.Trim() ?? string.Empty, Gender.Female);
        else if (!string.IsNullOrWhiteSpace(dto.BrideName))
            bridePersonId = await FindOrCreatePersonByNameAsync(dto.BrideName, Gender.Female);

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
        await SyncFamilyLinksAsync(wedding.Id);
        await SyncPersonRelationshipsAsync(wedding.Id);

        return await GetByIdAsync(wedding.Id);
    }

    public async Task<WeddingDto> UpdateRolesAsync(int id, WeddingFamilyTreeDto dto)
    {
        var (wedding, _) = await LoadFullWedding(id);

        // Snapshot person IDs before the update so stale relationships can be cleaned up
        var previousPersonIds = wedding.Roles
            .Where(r => r.PersonId.HasValue)
            .Select(r => r.PersonId!.Value)
            .ToHashSet();

        foreach (var slotInput in dto.Roles)
        {
            var role = wedding.Roles.FirstOrDefault(r => r.RoleType == slotInput.RoleType);
            if (role == null) continue;

            var oldPersonId = role.PersonId;
            var roleGender = GenderForRole(slotInput.RoleType);

            if (slotInput.PersonId.HasValue)
                role.PersonId = slotInput.PersonId;
            else if (!string.IsNullOrWhiteSpace(slotInput.FreeTextName))
                role.PersonId = await FindOrCreatePersonByNameAsync(slotInput.FreeTextName, roleGender);
            else if (role.RoleType is RoleType.Groom or RoleType.Bride && role.PersonId.HasValue)
                continue; // never silently clear Groom/Bride — they require an explicit replacement
            else
                role.PersonId = null;

            // Auto-set gender on the assigned person if it is still Unknown
            if (role.PersonId.HasValue && roleGender != Gender.Unknown)
            {
                var person = await _db.People.FindAsync(role.PersonId.Value);
                if (person != null && person.Gender == Gender.Unknown)
                    person.Gender = roleGender;
            }

            // SongAssignments are already loaded by LoadFullWedding
            if (role.PersonId != oldPersonId)
                _db.WeddingRoleSongAssignments.RemoveRange(role.SongAssignments);
        }

        wedding.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await SyncFamilyLinksAsync(id);
        await SyncPersonRelationshipsAsync(id, previousPersonIds);
        return await GetByIdAsync(id);
    }

    private async Task SyncFamilyLinksAsync(int weddingId)
    {
        var roles = await _db.WeddingRoles
            .Where(r => r.WeddingId == weddingId && r.PersonId.HasValue)
            .ToDictionaryAsync(r => r.RoleType, r => r.PersonId!.Value);

        var linkPairs = new[]
        {
            (Child: RoleType.Groom,         Father: RoleType.FatherOfGroom,               Mother: RoleType.MotherOfGroom),
            (Child: RoleType.Bride,         Father: RoleType.FatherOfBride,               Mother: RoleType.MotherOfBride),
            (Child: RoleType.FatherOfGroom, Father: RoleType.PaternalGrandfatherOfGroom,  Mother: RoleType.PaternalGrandmotherOfGroom),
            (Child: RoleType.MotherOfGroom, Father: RoleType.MaternalGrandfatherOfGroom,  Mother: RoleType.MaternalGrandmotherOfGroom),
            (Child: RoleType.FatherOfBride, Father: RoleType.PaternalGrandfatherOfBride,  Mother: RoleType.PaternalGrandmotherOfBride),
            (Child: RoleType.MotherOfBride, Father: RoleType.MaternalGrandfatherOfBride,  Mother: RoleType.MaternalGrandmotherOfBride),
        };

        var changed = false;
        foreach (var (childRole, fatherRole, motherRole) in linkPairs)
        {
            if (!roles.TryGetValue(childRole, out var childId)) continue;
            var child = await _db.People.FindAsync(childId);
            if (child == null) continue;

            if (!child.FatherId.HasValue && roles.TryGetValue(fatherRole, out var fatherId))
            {
                child.FatherId = fatherId;
                changed = true;
            }
            if (!child.MotherId.HasValue && roles.TryGetValue(motherRole, out var motherId))
            {
                child.MotherId = motherId;
                changed = true;
            }
        }

        if (changed) await _db.SaveChangesAsync();
    }

    private async Task SyncPersonRelationshipsAsync(int weddingId, HashSet<int>? previousPersonIds = null)
    {
        var roles = await _db.WeddingRoles
            .Where(r => r.WeddingId == weddingId && r.PersonId.HasValue)
            .ToDictionaryAsync(r => r.RoleType, r => r.PersonId!.Value);

        // Build the complete expected set of sync-managed relationships for this wedding.
        // Type IDs: 1=FATHER,2=MOTHER,3=SON,4=DAUGHTER,5=HUSBAND,6=WIFE,
        //           9=GRANDFATHER,10=GRANDMOTHER,11=GRANDSON,12=GRANDDAUGHTER,
        //           18=FATHER_IN_LAW,19=MOTHER_IN_LAW,20=SON_IN_LAW,21=DAUGHTER_IN_LAW
        var expected = new HashSet<(int From, int To, int Type)>();

        var parentChildPairs = new (RoleType child, RoleType parent, int childTypeId, int parentTypeId)[]
        {
            (RoleType.Groom,         RoleType.FatherOfGroom,              3,  1),
            (RoleType.Groom,         RoleType.MotherOfGroom,              3,  2),
            (RoleType.Bride,         RoleType.FatherOfBride,              4,  1),
            (RoleType.Bride,         RoleType.MotherOfBride,              4,  2),
            (RoleType.FatherOfGroom, RoleType.PaternalGrandfatherOfGroom, 3,  1),
            (RoleType.FatherOfGroom, RoleType.PaternalGrandmotherOfGroom, 3,  2),
            (RoleType.MotherOfGroom, RoleType.MaternalGrandfatherOfGroom, 4,  1),
            (RoleType.MotherOfGroom, RoleType.MaternalGrandmotherOfGroom, 4,  2),
            (RoleType.FatherOfBride, RoleType.PaternalGrandfatherOfBride, 3,  1),
            (RoleType.FatherOfBride, RoleType.PaternalGrandmotherOfBride, 3,  2),
            (RoleType.MotherOfBride, RoleType.MaternalGrandfatherOfBride, 4,  1),
            (RoleType.MotherOfBride, RoleType.MaternalGrandmotherOfBride, 4,  2),
            // Groom ↔ all four of his grandparents
            (RoleType.Groom,         RoleType.PaternalGrandfatherOfGroom, 11, 9),
            (RoleType.Groom,         RoleType.PaternalGrandmotherOfGroom, 11, 10),
            (RoleType.Groom,         RoleType.MaternalGrandfatherOfGroom, 11, 9),
            (RoleType.Groom,         RoleType.MaternalGrandmotherOfGroom, 11, 10),
            // Bride ↔ all four of her grandparents
            (RoleType.Bride,         RoleType.PaternalGrandfatherOfBride, 12, 9),
            (RoleType.Bride,         RoleType.PaternalGrandmotherOfBride, 12, 10),
            (RoleType.Bride,         RoleType.MaternalGrandfatherOfBride, 12, 9),
            (RoleType.Bride,         RoleType.MaternalGrandmotherOfBride, 12, 10),
        };

        foreach (var (childRole, parentRole, childTypeId, parentTypeId) in parentChildPairs)
        {
            if (!roles.TryGetValue(childRole, out var childId) || !roles.TryGetValue(parentRole, out var parentId)) continue;
            expected.Add((childId, parentId, childTypeId));
            expected.Add((parentId, childId, parentTypeId));
        }

        // Spouse pairs: Groom↔Bride plus each parent couple and each grandparent couple
        var spouseRolePairs = new (RoleType husband, RoleType wife)[]
        {
            (RoleType.Groom,                        RoleType.Bride),
            (RoleType.FatherOfGroom,                RoleType.MotherOfGroom),
            (RoleType.FatherOfBride,                RoleType.MotherOfBride),
            (RoleType.PaternalGrandfatherOfGroom,   RoleType.PaternalGrandmotherOfGroom),
            (RoleType.MaternalGrandfatherOfGroom,   RoleType.MaternalGrandmotherOfGroom),
            (RoleType.PaternalGrandfatherOfBride,   RoleType.PaternalGrandmotherOfBride),
            (RoleType.MaternalGrandfatherOfBride,   RoleType.MaternalGrandmotherOfBride),
        };
        foreach (var (husbandRole, wifeRole) in spouseRolePairs)
        {
            if (!roles.TryGetValue(husbandRole, out var hId) || !roles.TryGetValue(wifeRole, out var wId)) continue;
            expected.Add((hId, wId, 5)); // HUSBAND
            expected.Add((wId, hId, 6)); // WIFE
        }

        var inLawPairs = new (RoleType person, RoleType inLaw, int personTypeId, int inLawTypeId)[]
        {
            (RoleType.Groom, RoleType.FatherOfBride, 20, 18),
            (RoleType.Groom, RoleType.MotherOfBride, 20, 19),
            (RoleType.Bride, RoleType.FatherOfGroom, 21, 18),
            (RoleType.Bride, RoleType.MotherOfGroom, 21, 19),
        };

        foreach (var (person, inLaw, personTypeId, inLawTypeId) in inLawPairs)
        {
            if (!roles.TryGetValue(person, out var personId) || !roles.TryGetValue(inLaw, out var inLawId)) continue;
            expected.Add((personId, inLawId, personTypeId));
            expected.Add((inLawId, personId, inLawTypeId));
        }

        // Load all existing sync-managed relationships for everyone involved
        var syncTypeIds = new HashSet<int> { 1, 2, 3, 4, 5, 6, 9, 10, 11, 12, 18, 19, 20, 21 };
        var allAffectedIds = roles.Values.ToHashSet();
        if (previousPersonIds != null) allAffectedIds.UnionWith(previousPersonIds);

        // AND condition: only touch relationships where BOTH parties belong to this wedding's
        // affected set. OR would load relationships with people from other weddings and delete them.
        var existing = allAffectedIds.Count == 0
            ? new List<PersonRelationship>()
            : await _db.PersonRelationships
                .Where(r => allAffectedIds.Contains(r.FromPersonId)
                         && allAffectedIds.Contains(r.ToPersonId)
                         && syncTypeIds.Contains(r.RelationshipTypeId))
                .ToListAsync();

        // Remove only truly stale entries (in DB but not expected by current wedding state)
        var toRemove = existing
            .Where(r => !expected.Contains((r.FromPersonId, r.ToPersonId, r.RelationshipTypeId)))
            .ToList();
        if (toRemove.Count > 0)
        {
            _db.PersonRelationships.RemoveRange(toRemove);
            await _db.SaveChangesAsync();
        }

        // Add only missing relationships (expected but absent from DB)
        var existingSet = existing
            .Select(r => (r.FromPersonId, r.ToPersonId, r.RelationshipTypeId))
            .ToHashSet();
        var added = false;
        foreach (var (from, to, type) in expected)
        {
            if (from == to || existingSet.Contains((from, to, type))) continue;
            _db.PersonRelationships.Add(new PersonRelationship
            {
                FromPersonId = from, ToPersonId = to,
                RelationshipTypeId = type, IsActive = true, CreatedAt = DateTime.UtcNow
            });
            added = true;
        }
        if (added) await _db.SaveChangesAsync();
    }

    public async Task<WeddingDto> AssignSongsAsync(int id, AssignSongsDto dto)
    {
        var (wedding, _) = await LoadFullWedding(id);

        var conflictReport = await _conflicts.GetConflictReportAsync(id);
        var forbiddenIds = conflictReport.ForbiddenSongIds.ToHashSet();

        // Validate all incoming assignments before making any changes
        foreach (var input in dto.Assignments)
        {
            if (forbiddenIds.Contains(input.SongId))
                throw new DomainException($"Song {input.SongId} is forbidden for this wedding.");
            if (!wedding.Roles.Any(r => r.Id == input.WeddingRoleId))
                throw new KeyNotFoundException($"WeddingRole {input.WeddingRoleId} not found.");
        }

        // Remove all existing assignments so cleared slots are actually deleted
        foreach (var role in wedding.Roles)
        {
            _db.RemoveRange(role.SongAssignments);
            role.SongAssignments.Clear();
        }

        // Re-add only the assignments the client sent
        foreach (var input in dto.Assignments)
        {
            var role = wedding.Roles.First(r => r.Id == input.WeddingRoleId);
            role.SongAssignments.Add(new WeddingRoleSongAssignment
            {
                SongId = input.SongId,
                AssignmentSlot = input.AssignmentSlot
            });
        }

        wedding.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return await BuildWeddingDtoAsync(id, conflictReport);
    }

    public async Task<WeddingDto> FinalizeAsync(int id)
    {
        var wedding = await _db.Weddings.FindAsync(id)
            ?? throw new KeyNotFoundException($"Wedding {id} not found.");
        wedding.IsFinalized = true;
        wedding.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<WeddingDto> UnfinalizeAsync(int id)
    {
        var wedding = await _db.Weddings.FindAsync(id)
            ?? throw new KeyNotFoundException($"Wedding {id} not found.");
        wedding.IsFinalized = false;
        wedding.UpdatedDate = DateTime.Now;
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

    public async Task<List<(string roleLabel, string personName, string songTitle, string filePath)>> GetCombinedExportDataAsync(int weddingId)
    {
        var exists = await _db.Weddings.AnyAsync(w => w.Id == weddingId);
        if (!exists) throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var roles = await _db.WeddingRoles
            .Where(r => r.WeddingId == weddingId)
            .Include(r => r.Person)
            .Include(r => r.SongAssignments).ThenInclude(a => a.Song)
            .AsSplitQuery()
            .OrderBy(r => r.RoleType)
            .ToListAsync();

        var roleMap = roles.ToDictionary(r => r.RoleType);
        var result = new List<(string, string, string, string)>();

        // Slot-2 mother roles are skipped — their intro is combined with the father entry
        var skipSlot2 = new HashSet<RoleType> { RoleType.MotherOfGroom, RoleType.MotherOfBride };

        foreach (var role in roles)
        {
            foreach (var assignment in role.SongAssignments.OrderBy(a => a.AssignmentSlot))
            {
                if (assignment.AssignmentSlot == 2 && skipSlot2.Contains(role.RoleType))
                    continue;

                if (assignment.AssignmentSlot == 2 &&
                    (role.RoleType == RoleType.FatherOfGroom || role.RoleType == RoleType.FatherOfBride))
                {
                    var motherType = role.RoleType == RoleType.FatherOfGroom
                        ? RoleType.MotherOfGroom
                        : RoleType.MotherOfBride;
                    var motherName = roleMap.TryGetValue(motherType, out var mr) ? mr.Person?.FullName : null;
                    var fatherName = role.Person?.FullName;
                    var combined = string.Join(" & ",
                        new[] { fatherName, motherName }.Where(n => !string.IsNullOrWhiteSpace(n)));
                    result.Add(("Intro for Father/Mother", combined, assignment.Song.Title, assignment.Song.RelativeFilePath));
                }
                else
                {
                    result.Add((RoleHelper.GetLabel(role.RoleType), role.Person?.FullName ?? string.Empty, assignment.Song.Title, assignment.Song.RelativeFilePath));
                }
            }
        }

        return result;
    }

    private async Task<WeddingDto> BuildWeddingDtoAsync(int id, ConflictReportDto conflictReport)
    {
        var (wedding, songsByCategory) = await LoadFullWedding(id);
        return MapWeddingDto(wedding, conflictReport, songsByCategory);
    }

    private async Task<int> FindOrCreatePersonByFirstLastAsync(string firstName, string lastName, Gender gender = Gender.Unknown)
    {
        var existing = await _db.People
            .FirstOrDefaultAsync(p => p.FirstName == firstName && p.LastName == lastName);
        if (existing != null)
        {
            if (gender != Gender.Unknown && existing.Gender == Gender.Unknown)
                existing.Gender = gender;
            return existing.Id;
        }

        var person = new Person { FirstName = firstName, LastName = lastName, Gender = gender };
        _db.People.Add(person);
        await _db.SaveChangesAsync();
        return person.Id;
    }

    private async Task<int> FindOrCreatePersonByNameAsync(string name, Gender gender = Gender.Unknown)
    {
        var trimmed = name.Trim();
        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = parts.Length > 0 ? parts[0] : trimmed;
        var lastName = parts.Length > 1 ? parts[1] : string.Empty;

        var existing = await _db.People
            .FirstOrDefaultAsync(p => p.FirstName == firstName && p.LastName == lastName);
        if (existing != null)
        {
            if (gender != Gender.Unknown && existing.Gender == Gender.Unknown)
                existing.Gender = gender;
            return existing.Id;
        }

        try
        {
            var person = new Person { FirstName = firstName, LastName = lastName, Gender = gender };
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

    private static Gender GenderForRole(RoleType roleType) => roleType switch
    {
        RoleType.Groom                       => Gender.Male,
        RoleType.FatherOfGroom               => Gender.Male,
        RoleType.PaternalGrandfatherOfGroom  => Gender.Male,
        RoleType.MaternalGrandfatherOfGroom  => Gender.Male,
        RoleType.FatherOfBride               => Gender.Male,
        RoleType.PaternalGrandfatherOfBride  => Gender.Male,
        RoleType.MaternalGrandfatherOfBride  => Gender.Male,
        RoleType.Bride                       => Gender.Female,
        RoleType.MotherOfGroom               => Gender.Female,
        RoleType.PaternalGrandmotherOfGroom  => Gender.Female,
        RoleType.MaternalGrandmotherOfGroom  => Gender.Female,
        RoleType.MotherOfBride               => Gender.Female,
        RoleType.PaternalGrandmotherOfBride  => Gender.Female,
        RoleType.MaternalGrandmotherOfBride  => Gender.Female,
        _                                    => Gender.Unknown,
    };

    private async Task<(Wedding, Dictionary<int, List<Song>>)> LoadFullWedding(int id)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .Include(w => w.Roles).ThenInclude(r => r.SongAssignments).ThenInclude(a => a.Song).ThenInclude(s => s.Category)
            .AsSplitQuery()
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
            CreatedUtc = w.CreatedUtc,
            UpdatedDate = w.UpdatedDate,
            Roles = w.Roles.OrderBy(r => r.RoleType).Select(r => MapRoleDto(r, conflictReport, songsByCategory)).ToList(),
            ConflictReport = conflictReport
        };
    }

    private static WeddingRoleDto MapRoleDto(WeddingRole role, ConflictReportDto? conflictReport, Dictionary<int, List<Song>> songsByCategory)
    {
        var forbiddenIds = conflictReport?.ForbiddenSongIds.ToHashSet() ?? new HashSet<int>();
        var requiredSlots = RoleHelper.GetRequiredSlots(role.RoleType);
        var primaryCategoryIds = requiredSlots.Select(s => s.categoryId).ToHashSet();
        var categorySlotMap = requiredSlots.ToDictionary(x => x.categoryId, x => x.slot);

        var availableSongs = songsByCategory.Values
            .SelectMany(songs => songs.Select(s => new AvailableSongDto
            {
                SongId = s.Id,
                Title = s.Title,
                CategoryId = s.SongCategoryId,
                CategoryName = s.Category?.Name ?? string.Empty,
                AssignmentSlot = categorySlotMap.TryGetValue(s.SongCategoryId, out var sl) ? sl : 1,
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
            PersonFirstName = role.Person?.FirstName,
            PersonLastName = role.Person?.LastName,
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
