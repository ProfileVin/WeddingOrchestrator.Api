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
    private readonly IFamilyTreeService _familyTree;

    public WeddingService(AppDbContext db, IConflictDetectionService conflicts, IFamilyTreeService familyTree)
    {
        _db = db;
        _conflicts = conflicts;
        _familyTree = familyTree;
    }

    public async Task<List<WeddingListItemDto>> GetAllAsync()
    {
        var weddings = await _db.Weddings
            .Include(w => w.Details).ThenInclude(d => d.Person)
            .OrderByDescending(w => w.DateOfWedding)
            .ToListAsync();

        return weddings.Select(MapToListItemDto).ToList();
    }

    public async Task<List<WeddingListItemDto>> CheckAvailabilityAsync(DateTime date, TimeOnly? startTime, TimeOnly? endTime)
    {
        var startOfDay = date.Date;
        var endOfDay   = startOfDay.AddDays(1);

        var weddings = await _db.Weddings
            .Include(w => w.Details).ThenInclude(d => d.Person)
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
        var groom = w.Details.FirstOrDefault(d => d.RoleType == RoleType.Groom);
        var bride = w.Details.FirstOrDefault(d => d.RoleType == RoleType.Bride);
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

        var details = RoleHelper.AllRoles
            .Where(rt => rt != RoleType.OtherRelation && rt != RoleType.WeddingItself)
            .Select(rt => new WeddingDetail
            {
                WeddingId = wedding.Id,
                RoleType = rt,
                PersonId = rt == RoleType.Groom ? groomPersonId
                         : rt == RoleType.Bride ? bridePersonId
                         : null
            }).ToList();

        if (dto.WeddingIntroSongId.HasValue || !string.IsNullOrWhiteSpace(dto.Notes))
            details.Add(new WeddingDetail
            {
                WeddingId = wedding.Id,
                RoleType = RoleType.WeddingItself,
                SongId = dto.WeddingIntroSongId,
                Note = dto.Notes?.Trim()
            });

        _db.WeddingDetails.AddRange(details);
        await _db.SaveChangesAsync();
        await SyncFamilyLinksAsync(wedding.Id);
        await SyncPersonRelationshipsAsync(wedding.Id);

        return await GetByIdAsync(wedding.Id);
    }

    public async Task<WeddingDto> UpdateRolesAsync(int id, WeddingFamilyTreeDto dto)
    {
        var wedding = await _db.Weddings.FindAsync(id)
            ?? throw new KeyNotFoundException($"Wedding {id} not found.");

        var details = await _db.WeddingDetails
            .Where(d => d.WeddingId == id)
            .ToListAsync();

        var previousPersonIds = details
            .Where(d => d.PersonId.HasValue)
            .Select(d => d.PersonId!.Value)
            .ToHashSet();

        foreach (var slotInput in dto.Roles)
        {
            if (slotInput.RoleType == RoleType.OtherRelation)
            {
                int? otherPersonId = null;
                if (slotInput.PersonId.HasValue)
                    otherPersonId = slotInput.PersonId;
                else if (!string.IsNullOrWhiteSpace(slotInput.FreeTextName))
                    otherPersonId = await FindOrCreatePersonByNameAsync(slotInput.FreeTextName, Gender.Unknown);

                if (otherPersonId.HasValue)
                {
                    var existing = details.FirstOrDefault(d => d.PersonId == otherPersonId && d.RoleType == RoleType.OtherRelation);
                    if (existing == null)
                    {
                        var newDetail = new WeddingDetail
                        {
                            WeddingId = id,
                            PersonId = otherPersonId,
                            RoleType = RoleType.OtherRelation,
                            InWeddingRelationTypeId = slotInput.RelationshipTypeId
                        };
                        _db.WeddingDetails.Add(newDetail);
                        details.Add(newDetail);
                    }
                    else if (slotInput.RelationshipTypeId.HasValue)
                    {
                        existing.InWeddingRelationTypeId = slotInput.RelationshipTypeId;
                    }
                }
                continue;
            }

            var detail = details.FirstOrDefault(d => d.RoleType == slotInput.RoleType);
            if (detail == null) continue;

            var oldPersonId = detail.PersonId;
            var roleGender = GenderForRole(slotInput.RoleType);

            if (slotInput.PersonId.HasValue)
                detail.PersonId = slotInput.PersonId;
            else if (!string.IsNullOrWhiteSpace(slotInput.FreeTextName))
                detail.PersonId = await FindOrCreatePersonByNameAsync(slotInput.FreeTextName, roleGender);
            else if (slotInput.RoleType is RoleType.Groom or RoleType.Bride && detail.PersonId.HasValue)
                continue;
            else
                detail.PersonId = null;

            if (detail.PersonId.HasValue && roleGender != Gender.Unknown)
            {
                var person = await _db.People.FindAsync(detail.PersonId.Value);
                if (person != null && person.Gender == Gender.Unknown)
                    person.Gender = roleGender;
            }

            if (detail.PersonId != oldPersonId)
                detail.SongId = null;
        }

        if (dto.Notes != null)
        {
            var weddingItselfDetail = details.FirstOrDefault(d => d.RoleType == RoleType.WeddingItself);
            if (weddingItselfDetail != null)
                weddingItselfDetail.Note = dto.Notes.Trim();
            else
                _db.WeddingDetails.Add(new WeddingDetail
                {
                    WeddingId = id,
                    RoleType = RoleType.WeddingItself,
                    Note = dto.Notes.Trim()
                });
        }

        wedding.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        await SyncFamilyLinksAsync(id);
        await SyncPersonRelationshipsAsync(id, previousPersonIds);
        await SyncOtherRelationPersonRelationshipsAsync(id);
        return await GetByIdAsync(id);
    }

    private async Task SyncFamilyLinksAsync(int weddingId)
    {
        var roles = await _db.WeddingDetails
            .Where(d => d.WeddingId == weddingId && d.PersonId.HasValue && d.RoleType != RoleType.OtherRelation)
            .ToDictionaryAsync(d => d.RoleType, d => d.PersonId!.Value);

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
        var roles = await _db.WeddingDetails
            .Where(d => d.WeddingId == weddingId && d.PersonId.HasValue && d.RoleType != RoleType.OtherRelation)
            .ToDictionaryAsync(d => d.RoleType, d => d.PersonId!.Value);

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
            (RoleType.Groom,         RoleType.PaternalGrandfatherOfGroom, 11, 9),
            (RoleType.Groom,         RoleType.PaternalGrandmotherOfGroom, 11, 10),
            (RoleType.Groom,         RoleType.MaternalGrandfatherOfGroom, 11, 9),
            (RoleType.Groom,         RoleType.MaternalGrandmotherOfGroom, 11, 10),
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
            expected.Add((hId, wId, 5));
            expected.Add((wId, hId, 6));
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

        var parentGrandparentInLawPairs = new (RoleType parent, RoleType grandparent, int parentTypeId, int grandparentTypeId)[]
        {
            (RoleType.MotherOfGroom, RoleType.PaternalGrandfatherOfGroom, 21, 18),
            (RoleType.MotherOfGroom, RoleType.PaternalGrandmotherOfGroom, 21, 19),
            (RoleType.FatherOfGroom, RoleType.MaternalGrandfatherOfGroom, 20, 18),
            (RoleType.FatherOfGroom, RoleType.MaternalGrandmotherOfGroom, 20, 19),
            (RoleType.MotherOfBride, RoleType.PaternalGrandfatherOfBride, 21, 18),
            (RoleType.MotherOfBride, RoleType.PaternalGrandmotherOfBride, 21, 19),
            (RoleType.FatherOfBride, RoleType.MaternalGrandfatherOfBride, 20, 18),
            (RoleType.FatherOfBride, RoleType.MaternalGrandmotherOfBride, 20, 19),
        };
        foreach (var (parentRole, grandparentRole, parentTypeId, grandparentTypeId) in parentGrandparentInLawPairs)
        {
            if (!roles.TryGetValue(parentRole, out var parentId) || !roles.TryGetValue(grandparentRole, out var grandparentId)) continue;
            expected.Add((parentId, grandparentId, parentTypeId));
            expected.Add((grandparentId, parentId, grandparentTypeId));
        }

        var grandchildInLawPairs = new (RoleType grandchild, RoleType grandparent, int grandchildTypeId, int grandparentTypeId)[]
        {
            (RoleType.Bride, RoleType.PaternalGrandfatherOfGroom, 41, 42),
            (RoleType.Bride, RoleType.PaternalGrandmotherOfGroom, 41, 43),
            (RoleType.Bride, RoleType.MaternalGrandfatherOfGroom, 41, 42),
            (RoleType.Bride, RoleType.MaternalGrandmotherOfGroom, 41, 43),
            (RoleType.Groom, RoleType.PaternalGrandfatherOfBride, 40, 42),
            (RoleType.Groom, RoleType.PaternalGrandmotherOfBride, 40, 43),
            (RoleType.Groom, RoleType.MaternalGrandfatherOfBride, 40, 42),
            (RoleType.Groom, RoleType.MaternalGrandmotherOfBride, 40, 43),
        };
        foreach (var (grandchildRole, grandparentRole, grandchildTypeId, grandparentTypeId) in grandchildInLawPairs)
        {
            if (!roles.TryGetValue(grandchildRole, out var grandchildId) || !roles.TryGetValue(grandparentRole, out var grandparentId)) continue;
            expected.Add((grandchildId, grandparentId, grandchildTypeId));
            expected.Add((grandparentId, grandchildId, grandparentTypeId));
        }

        var syncTypeIds = new HashSet<int> { 1, 2, 3, 4, 5, 6, 9, 10, 11, 12, 18, 19, 20, 21, 40, 41, 42, 43 };
        var allAffectedIds = roles.Values.ToHashSet();
        if (previousPersonIds != null) allAffectedIds.UnionWith(previousPersonIds);

        var existing = allAffectedIds.Count == 0
            ? new List<PersonRelationship>()
            : await _db.PersonRelationships
                .Where(r => allAffectedIds.Contains(r.FromPersonId)
                         && allAffectedIds.Contains(r.ToPersonId)
                         && syncTypeIds.Contains(r.RelationshipTypeId))
                .ToListAsync();

        var toRemove = existing
            .Where(r => !expected.Contains((r.FromPersonId, r.ToPersonId, r.RelationshipTypeId)))
            .ToList();
        if (toRemove.Count > 0)
        {
            _db.PersonRelationships.RemoveRange(toRemove);
            await _db.SaveChangesAsync();
        }

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
        var details = await _db.WeddingDetails
            .Where(d => d.WeddingId == id)
            .ToListAsync();

        var conflictReport = await _conflicts.GetConflictReportAsync(id);
        var forbiddenIds = conflictReport.ForbiddenSongIds.ToHashSet();

        foreach (var input in dto.Assignments)
        {
            if (forbiddenIds.Contains(input.SongId))
                throw new DomainException($"Song {input.SongId} is forbidden for this wedding.");

            var detail = ResolveDetail(details, input);
            if (detail == null)
                throw new KeyNotFoundException($"WeddingDetail {input.WeddingRoleId} not found.");
        }

        foreach (var d in details)
            d.SongId = null;

        foreach (var input in dto.Assignments.Where(a => a.AssignmentSlot == 1))
        {
            var detail = ResolveDetail(details, input);
            if (detail != null)
                detail.SongId = input.SongId;
        }

        var wedding = await _db.Weddings.FindAsync(id);
        if (wedding != null) wedding.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync();
        return await BuildWeddingDtoAsync(id, conflictReport);
    }

    private static WeddingDetail? ResolveDetail(List<WeddingDetail> details, SongAssignmentInputDto input)
    {
        if (input.PersonId.HasValue && input.WeddingRoleId == 0)
            return details.FirstOrDefault(d => d.RoleType == RoleType.OtherRelation && d.PersonId == input.PersonId);
        return details.FirstOrDefault(d => d.Id == input.WeddingRoleId);
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

    public async Task<WeddingDto> LinkOtherRelationsAsync(int weddingId, List<LinkOtherRelationDto> items)
    {
        var exists = await _db.Weddings.AnyAsync(w => w.Id == weddingId);
        if (!exists) throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        foreach (var item in items)
        {
            int personId;
            if (item.PersonId.HasValue)
            {
                personId = item.PersonId.Value;
            }
            else if (!string.IsNullOrWhiteSpace(item.FirstName))
            {
                personId = await FindOrCreatePersonByFirstLastAsync(
                    item.FirstName.Trim(),
                    item.LastName?.Trim() ?? string.Empty,
                    Gender.Unknown);
            }
            else continue;

            var existing = await _db.WeddingDetails
                .FirstOrDefaultAsync(d => d.WeddingId == weddingId && d.PersonId == personId && d.RoleType == RoleType.OtherRelation);

            if (existing == null)
            {
                _db.WeddingDetails.Add(new WeddingDetail
                {
                    WeddingId = weddingId,
                    PersonId = personId,
                    RoleType = RoleType.OtherRelation,
                    InWeddingRelationTypeId = item.RelationshipTypeId > 0 ? item.RelationshipTypeId : null,
                    WeddingSide = item.WeddingSide,
                    RelatedToPersonId = item.RelatedPersonId
                });
            }
            else
            {
                if (item.RelationshipTypeId > 0)
                    existing.InWeddingRelationTypeId = item.RelationshipTypeId;
                if (item.WeddingSide != null)
                    existing.WeddingSide = item.WeddingSide;
                if (item.RelatedPersonId.HasValue)
                    existing.RelatedToPersonId = item.RelatedPersonId;
            }
        }

        await _db.SaveChangesAsync();

        // Build the full family chain for each OtherRelation (parents, grandparents,
        // siblings, spouse in-laws). DB-derived discovery works here because
        // updateRoles/SyncPersonRelationshipsAsync has already populated all standard
        // PersonRelationship rows before this call is made.
        foreach (var item in items)
        {
            if (!item.PersonId.HasValue || !item.RelatedPersonId.HasValue || item.RelationshipTypeId <= 0)
                continue;

            var relType = await _db.RelationshipTypes
                .FirstOrDefaultAsync(rt => rt.Id == item.RelationshipTypeId && rt.IsActive);
            if (relType == null) continue;

            await _familyTree.BuildRelationshipsForExistingPersonAsync(
                item.PersonId.Value, item.RelatedPersonId.Value, relType.TypeCode, weddingId);
        }

        await SyncOtherRelationPersonRelationshipsAsync(weddingId);
        return await GetByIdAsync(weddingId);
    }

    public async Task DeleteOtherRelationAsync(int weddingId, int personId)
    {
        var detail = await _db.WeddingDetails
            .FirstOrDefaultAsync(d => d.WeddingId == weddingId && d.PersonId == personId && d.RoleType == RoleType.OtherRelation);
        if (detail == null) return;
        _db.WeddingDetails.Remove(detail);

        var relationships = await _db.PersonRelationships
            .Where(r => r.FromPersonId == personId || r.ToPersonId == personId)
            .ToListAsync();
        _db.PersonRelationships.RemoveRange(relationships);

        var person = await _db.People.FindAsync(personId);
        if (person != null) _db.People.Remove(person);

        await _db.SaveChangesAsync();
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
        var detail = await _db.WeddingDetails
            .Include(d => d.Song)
            .FirstOrDefaultAsync(d => d.WeddingId == weddingId && d.RoleType == roleType && d.SongId.HasValue);

        if (detail?.Song == null) return null;
        return (RoleHelper.GetLabel(roleType), detail.Song.RelativeFilePath, detail.Song.Title);
    }

    public async Task<List<(string roleLabel, string personName, string songTitle, string filePath)>> GetCombinedExportDataAsync(int weddingId)
    {
        var wedding = await _db.Weddings
            .Include(w => w.WeddingSongIntro)
            .Include(w => w.FatherMotherWeddingSongIntroGroom)
            .Include(w => w.FatherMotherWeddingSongIntroBride)
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var details = await _db.WeddingDetails
            .Where(d => d.WeddingId == weddingId && d.SongId.HasValue)
            .Include(d => d.Person)
            .Include(d => d.Song)
            .OrderBy(d => d.RoleType)
            .ToListAsync();

        var result = details
            .Where(d => d.Song != null)
            .Select(d => (RoleHelper.GetLabel(d.RoleType), d.Person?.FullName ?? string.Empty, d.Song!.Title, d.Song!.RelativeFilePath))
            .ToList();

        if (wedding.WeddingSongIntro != null)
            result.Add(("Wedding Intro", string.Empty, wedding.WeddingSongIntro.Title, wedding.WeddingSongIntro.RelativeFilePath));
        if (wedding.FatherMotherWeddingSongIntroGroom != null)
            result.Add(("Father/Mother Groom Intro", string.Empty, wedding.FatherMotherWeddingSongIntroGroom.Title, wedding.FatherMotherWeddingSongIntroGroom.RelativeFilePath));
        if (wedding.FatherMotherWeddingSongIntroBride != null)
            result.Add(("Father/Mother Bride Intro", string.Empty, wedding.FatherMotherWeddingSongIntroBride.Title, wedding.FatherMotherWeddingSongIntroBride.RelativeFilePath));

        return result;
    }

    private async Task<WeddingDto> BuildWeddingDtoAsync(int id, ConflictReportDto conflictReport)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Details).ThenInclude(d => d.Person)
            .Include(w => w.Details).ThenInclude(d => d.Song!).ThenInclude(s => s.Category)
            .Include(w => w.Details).ThenInclude(d => d.WeddingRelationType)
            .Include(w => w.WeddingSongIntro)
            .Include(w => w.FatherMotherWeddingSongIntroGroom)
            .Include(w => w.FatherMotherWeddingSongIntroBride)
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

    private static WeddingDto MapWeddingDto(Wedding w, ConflictReportDto? conflictReport, Dictionary<int, List<Song>> songsByCategory)
    {
        var weddingItselfNote = w.Details.FirstOrDefault(d => d.RoleType == RoleType.WeddingItself)?.Note;
        var forbiddenIds = conflictReport?.ForbiddenSongIds.ToHashSet() ?? new HashSet<int>();

        var availableWeddingIntroSongs = songsByCategory.Values
            .SelectMany(songs => songs.Select(s => new AvailableSongDto
            {
                SongId = s.Id, Title = s.Title, CategoryId = s.SongCategoryId,
                CategoryName = s.Category?.Name ?? string.Empty, AssignmentSlot = 1,
                IsForbidden = forbiddenIds.Contains(s.Id), IsPrimaryCategory = false
            }))
            .OrderBy(s => s.CategoryName).ThenBy(s => s.Title)
            .ToList();

        var availableParentIntroSongs = songsByCategory.Values
            .SelectMany(songs => songs.Select(s => new AvailableSongDto
            {
                SongId = s.Id, Title = s.Title, CategoryId = s.SongCategoryId,
                CategoryName = s.Category?.Name ?? string.Empty, AssignmentSlot = 2,
                IsForbidden = forbiddenIds.Contains(s.Id), IsPrimaryCategory = false
            }))
            .OrderBy(s => s.CategoryName).ThenBy(s => s.Title)
            .ToList();

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
            Notes = weddingItselfNote,
            WeddingSongIntroId = w.WeddingSongIntroId,
            WeddingSongIntroTitle = w.WeddingSongIntro?.Title,
            FatherMotherWeddingSongIntroGroomId = w.FatherMotherWeddingSongIntroGroomId,
            FatherMotherWeddingSongIntroGroomTitle = w.FatherMotherWeddingSongIntroGroom?.Title,
            FatherMotherWeddingSongIntroBrideId = w.FatherMotherWeddingSongIntroBrideId,
            FatherMotherWeddingSongIntroBrideTitle = w.FatherMotherWeddingSongIntroBride?.Title,
            AvailableWeddingIntroSongs = availableWeddingIntroSongs,
            AvailableParentIntroSongs = availableParentIntroSongs,
            Roles = w.Details.OrderBy(d => d.RoleType).Select(d => MapDetailDto(d, conflictReport, songsByCategory)).ToList(),
            ConflictReport = conflictReport
        };
    }

    private static WeddingRoleDto MapDetailDto(WeddingDetail detail, ConflictReportDto? conflictReport, Dictionary<int, List<Song>> songsByCategory)
    {
        var forbiddenIds = conflictReport?.ForbiddenSongIds.ToHashSet() ?? new HashSet<int>();
        var requiredSlots = RoleHelper.GetRequiredSlots(detail.RoleType);
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

        var songAssignments = new List<SongAssignmentDto>();
        if (detail.SongId.HasValue && detail.Song != null)
        {
            songAssignments.Add(new SongAssignmentDto
            {
                AssignmentSlot = 1,
                SongId = detail.SongId.Value,
                SongTitle = detail.Song.Title,
                SongCategoryName = detail.Song.Category?.Name ?? string.Empty,
                FileSizeBytes = detail.Song.FileSizeBytes
            });
        }

        return new WeddingRoleDto
        {
            Id = detail.Id,
            RoleType = detail.RoleType,
            RoleLabel = RoleHelper.GetLabel(detail.RoleType),
            PersonId = detail.PersonId,
            PersonName = detail.Person?.FullName,
            PersonFirstName = detail.Person?.FirstName,
            PersonLastName = detail.Person?.LastName,
            InWeddingRelationTypeLabel = detail.WeddingRelationType?.TypeLabel,
            WeddingSide = detail.WeddingSide,
            Note = detail.Note,
            SongAssignments = songAssignments,
            AvailableSongs = availableSongs
        };
    }

    public async Task<WeddingDto> UpdateWeddingSongIntrosAsync(int weddingId, UpdateWeddingSongIntrosDto dto)
    {
        var wedding = await _db.Weddings.FindAsync(weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        wedding.WeddingSongIntroId = dto.WeddingSongIntroId;
        wedding.FatherMotherWeddingSongIntroGroomId = dto.FatherMotherWeddingSongIntroGroomId;
        wedding.FatherMotherWeddingSongIntroBrideId = dto.FatherMotherWeddingSongIntroBrideId;
        wedding.UpdatedDate = DateTime.Now;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(weddingId);
    }

    public async Task<WeddingDto> UpdateDetailNoteAsync(int weddingId, UpdateDetailNoteDto dto)
    {
        var detail = dto.RoleType == RoleType.OtherRelation
            ? await _db.WeddingDetails.FirstOrDefaultAsync(d =>
                d.WeddingId == weddingId &&
                d.RoleType == RoleType.OtherRelation &&
                d.PersonId == dto.PersonId)
            : await _db.WeddingDetails.FirstOrDefaultAsync(d =>
                d.WeddingId == weddingId &&
                d.RoleType == dto.RoleType);

        if (detail == null)
            throw new KeyNotFoundException($"Wedding detail not found for wedding {weddingId}.");

        detail.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        await _db.SaveChangesAsync();
        return await GetByIdAsync(weddingId);
    }

    private async Task SyncOtherRelationPersonRelationshipsAsync(int weddingId)
    {
        var otherRows = await _db.WeddingDetails
            .Where(d => d.WeddingId == weddingId && d.PersonId.HasValue
                     && d.RoleType == RoleType.OtherRelation
                     && d.InWeddingRelationTypeId.HasValue
                     && d.RelatedToPersonId.HasValue)
            .ToListAsync();

        if (otherRows.Count == 0) return;

        var neededTypeIds = otherRows.Select(d => d.InWeddingRelationTypeId!.Value).Distinct().ToList();
        var typeById = await _db.RelationshipTypes
            .Where(rt => neededTypeIds.Contains(rt.Id))
            .ToDictionaryAsync(rt => rt.Id);
        var typeByCode = await _db.RelationshipTypes
            .ToDictionaryAsync(rt => rt.TypeCode, rt => rt.Id);

        var expected = new HashSet<(int From, int To, int TypeId)>();
        foreach (var row in otherRows)
        {
            var anchorId      = row.RelatedToPersonId!.Value;
            var otherPersonId = row.PersonId!.Value;
            var typeId        = row.InWeddingRelationTypeId!.Value;

            expected.Add((otherPersonId, anchorId, typeId));

            if (typeById.TryGetValue(typeId, out var rt))
            {
                var anchorGender = await _db.People
                    .Where(p => p.Id == anchorId)
                    .Select(p => (int?)p.Gender)
                    .FirstOrDefaultAsync();
                var anchorIsMale = anchorGender == 1;

                var invCode = GetInverseCode(rt.TypeCode, anchorIsMale);
                if (invCode != null && typeByCode.TryGetValue(invCode, out var invId))
                    expected.Add((anchorId, otherPersonId, invId));
            }
        }

        var allIds = expected.Select(e => e.From).Concat(expected.Select(e => e.To)).ToHashSet();
        var existingSet = (await _db.PersonRelationships
            .Where(r => allIds.Contains(r.FromPersonId) && allIds.Contains(r.ToPersonId))
            .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
            .ToListAsync())
            .Select(r => (r.FromPersonId, r.ToPersonId, r.RelationshipTypeId))
            .ToHashSet();

        var toAdd = expected
            .Where(e => !existingSet.Contains(e))
            .Select(e => new PersonRelationship
            {
                FromPersonId       = e.From,
                ToPersonId         = e.To,
                RelationshipTypeId = e.TypeId,
                IsActive           = true,
                CreatedAt          = DateTime.UtcNow
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            _db.PersonRelationships.AddRange(toAdd);
            await _db.SaveChangesAsync();
        }
    }

    private static string? GetInverseCode(string typeCode, bool anchorIsMale) => typeCode switch
    {
        "BROTHER"       => anchorIsMale ? "BROTHER"       : "SISTER",
        "SISTER"        => anchorIsMale ? "BROTHER"       : "SISTER",
        "SON"           => anchorIsMale ? "FATHER"        : "MOTHER",
        "DAUGHTER"      => anchorIsMale ? "FATHER"        : "MOTHER",
        "FATHER"        => anchorIsMale ? "SON"           : "DAUGHTER",
        "MOTHER"        => anchorIsMale ? "SON"           : "DAUGHTER",
        "UNCLE"         => anchorIsMale ? "NEPHEW"        : "NIECE",
        "AUNT"          => anchorIsMale ? "NEPHEW"        : "NIECE",
        "NEPHEW"        => anchorIsMale ? "UNCLE"         : "AUNT",
        "NIECE"         => anchorIsMale ? "UNCLE"         : "AUNT",
        "GRANDFATHER"   => anchorIsMale ? "GRANDSON"      : "GRANDDAUGHTER",
        "GRANDMOTHER"   => anchorIsMale ? "GRANDSON"      : "GRANDDAUGHTER",
        "GRANDSON"      => anchorIsMale ? "GRANDFATHER"   : "GRANDMOTHER",
        "GRANDDAUGHTER" => anchorIsMale ? "GRANDFATHER"   : "GRANDMOTHER",
        "COUSIN"        => "COUSIN",
        "STEP_BROTHER"  => anchorIsMale ? "STEP_BROTHER"  : "STEP_SISTER",
        "STEP_SISTER"   => anchorIsMale ? "STEP_BROTHER"  : "STEP_SISTER",
        "STEP_SON"      => anchorIsMale ? "STEP_FATHER"   : "STEP_MOTHER",
        "STEP_DAUGHTER" => anchorIsMale ? "STEP_FATHER"   : "STEP_MOTHER",
        "STEP_FATHER"   => anchorIsMale ? "STEP_SON"      : "STEP_DAUGHTER",
        "STEP_MOTHER"   => anchorIsMale ? "STEP_SON"      : "STEP_DAUGHTER",
        "HALF_BROTHER"  => anchorIsMale ? "HALF_BROTHER"  : "HALF_SISTER",
        "HALF_SISTER"   => anchorIsMale ? "HALF_BROTHER"  : "HALF_SISTER",
        _               => null
    };
}
