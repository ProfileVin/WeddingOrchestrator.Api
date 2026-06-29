using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.FamilyTree;
using WeddingOrchestrator.Api.DTOs.People;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class FamilyTreeService : IFamilyTreeService
{
    private readonly AppDbContext _db;
    private readonly IPersonService _personService;

    public FamilyTreeService(AppDbContext db, IPersonService personService)
    {
        _db = db;
        _personService = personService;
    }

    public async Task<FamilySummariesResponseDto> GetFamilySummariesAsync()
    {
        var allPeople = await _db.People
            .Select(p => new { p.Id, p.LastName })
            .ToListAsync();

        var totalPeople = allPeople.Count;

        var byLastName = allPeople
            .GroupBy(p => p.LastName.ToLower())
            .Select(g => (
                DisplayName: ToPascalCase(g.First().LastName),
                Ids: g.Select(p => p.Id).ToHashSet()
            ))
            .ToList();

        var allRels = await _db.PersonRelationships
            .Where(r => r.IsActive)
            .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
            .ToListAsync();

        var weddingRoles = await _db.WeddingDetails
            .Where(d => d.PersonId != null && d.RoleType != RoleType.OtherRelation)
            .Select(d => new { d.WeddingId, d.RoleType, PersonId = d.PersonId!.Value })
            .ToListAsync();

        var spouseTypeIds = new HashSet<int> { 5, 6 };
        var downwardTypeIds = new HashSet<int> { 3, 4, 20, 21, 26, 27, 32, 33 };
        var upwardTypeIds   = new HashSet<int> { 1, 2, 24, 25, 30, 31 };

        // For each wedding, map father → grandfather (or father is root when grandfather absent).
        // Father of Groom → Paternal Grandfather of Groom
        // Father of Bride → Paternal Grandfather of Bride
        var fatherToGrandfather = new Dictionary<int, int>();
        foreach (var wedding in weddingRoles.GroupBy(wr => wr.WeddingId))
        {
            var roles = wedding.ToList();

            var fog = roles.FirstOrDefault(r => r.RoleType == RoleType.FatherOfGroom);
            var gog = roles.FirstOrDefault(r => r.RoleType == RoleType.PaternalGrandfatherOfGroom);
            if (fog != null && gog != null)
                fatherToGrandfather[fog.PersonId] = gog.PersonId;

            var fob = roles.FirstOrDefault(r => r.RoleType == RoleType.FatherOfBride);
            var gob = roles.FirstOrDefault(r => r.RoleType == RoleType.PaternalGrandfatherOfBride);
            if (fob != null && gob != null)
                fatherToGrandfather[fob.PersonId] = gob.PersonId;
        }

        var fatherRoleTypes = new HashSet<RoleType> { RoleType.FatherOfGroom, RoleType.FatherOfBride };
        var allFatherPersonIds = weddingRoles
            .Where(wr => fatherRoleTypes.Contains(wr.RoleType))
            .Select(wr => wr.PersonId)
            .ToHashSet();

        var families = byLastName.Select(family =>
            {
                var familyIds = family.Ids;

                // Roots are anchored to wedding roles:
                // find fathers from this family, then resolve to grandfather (or father if no grandfather).
                var rootIds = new HashSet<int>();
                foreach (var fatherId in allFatherPersonIds.Where(id => familyIds.Contains(id)))
                {
                    rootIds.Add(fatherToGrandfather.TryGetValue(fatherId, out var gfId) ? gfId : fatherId);
                }

                // Fallback for families not referenced in any wedding role.
                // A root has no parent — nobody declared them as a child via FATHER/MOTHER (TO=them),
                // and they haven't declared a parent via SON/DAUGHTER (FROM=them).
                if (!rootIds.Any())
                {
                    var hasParent = allRels
                        .Where(r =>
                            (upwardTypeIds.Contains(r.RelationshipTypeId)   && familyIds.Contains(r.ToPersonId))   ||  // FATHER/MOTHER: TO=child
                            (downwardTypeIds.Contains(r.RelationshipTypeId) && familyIds.Contains(r.FromPersonId)))    // SON/DAUGHTER:  FROM=child
                        .Select(r => upwardTypeIds.Contains(r.RelationshipTypeId) ? r.ToPersonId : r.FromPersonId)
                        .ToHashSet();

                    rootIds = familyIds.Where(id => !hasParent.Contains(id)).ToHashSet();
                    if (!rootIds.Any()) rootIds = new HashSet<int>(familyIds);
                }

                // BFS from roots: count root + spouse + all descendants + spouses of descendants.
                // Traverse into same-family nodes; count-but-stop at married-out members.
                var counted = new HashSet<int>(rootIds);
                var queue   = new Queue<int>(rootIds);

                while (queue.Count > 0)
                {
                    var pid = queue.Dequeue();

                    // Spouses: always count, traverse only if same family last name
                    foreach (var sid in allRels
                        .Where(r => spouseTypeIds.Contains(r.RelationshipTypeId) &&
                                   (r.FromPersonId == pid || r.ToPersonId == pid))
                        .SelectMany(r => new[] { r.FromPersonId, r.ToPersonId })
                        .Where(id => id != pid && !counted.Contains(id)))
                    {
                        counted.Add(sid);
                        if (familyIds.Contains(sid))
                            queue.Enqueue(sid);
                    }

                    var childrenA = allRels
                        .Where(r => upwardTypeIds.Contains(r.RelationshipTypeId) && r.FromPersonId == pid)
                        .Select(r => r.ToPersonId);

                    var childrenB = allRels
                        .Where(r => downwardTypeIds.Contains(r.RelationshipTypeId) && r.ToPersonId == pid)
                        .Select(r => r.FromPersonId);

                    foreach (var cid in childrenA.Concat(childrenB).Distinct().Where(id => !counted.Contains(id)))
                    {
                        counted.Add(cid);
                        if (familyIds.Contains(cid))
                        {
                            queue.Enqueue(cid);
                        }
                        else
                        {
                            // Married-out child: count their spouse but don't traverse further
                            foreach (var gsid in allRels
                                .Where(r => spouseTypeIds.Contains(r.RelationshipTypeId) &&
                                           (r.FromPersonId == cid || r.ToPersonId == cid))
                                .SelectMany(r => new[] { r.FromPersonId, r.ToPersonId })
                                .Where(id => id != cid && !counted.Contains(id)))
                            {
                                counted.Add(gsid);
                            }
                        }
                    }
                }

                return new FamilySummaryDto { LastName = family.DisplayName, MemberCount = counted.Count };
            })
            .OrderBy(s => s.LastName)
            .ToList();

        return new FamilySummariesResponseDto { Families = families, TotalPeople = totalPeople };
    }

    private static string ToPascalCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1).ToLower();

    public async Task<FamilyTreeDataDto> GetFamilyTreeByLastNameAsync(string lastName)
    {
        var lower = lastName.ToLower();

        var familyPeople = await _db.People
            .Where(p => p.LastName.ToLower() == lower)
            .Select(p => new FamilyPersonDto
            {
                Id       = p.Id,
                FullName = p.FirstName + " " + p.LastName,
                FirstName = p.FirstName,
                LastName  = p.LastName,
                Gender    = p.Gender.ToString().ToLower(),
            })
            .ToListAsync();

        if (familyPeople.Count == 0)
            return new FamilyTreeDataDto();

        var familyIds = familyPeople.Select(p => p.Id).ToHashSet();

        // All active relationships where either participant belongs to this family
        var relationships = await _db.PersonRelationships
            .Include(r => r.FromPerson)
            .Include(r => r.ToPerson)
            .Include(r => r.RelationshipType)
            .Where(r => r.IsActive &&
                        (familyIds.Contains(r.FromPersonId) || familyIds.Contains(r.ToPersonId)))
            .Select(r => new PersonRelationshipDto
            {
                Id                 = r.Id,
                FromPersonId       = r.FromPersonId,
                FromPersonName     = r.FromPerson.FirstName + " " + r.FromPerson.LastName,
                ToPersonId         = r.ToPersonId,
                ToPersonName       = r.ToPerson.FirstName   + " " + r.ToPerson.LastName,
                RelationshipTypeId = r.RelationshipTypeId,
                TypeCode           = r.RelationshipType.TypeCode,
                TypeLabel          = r.RelationshipType.TypeLabel,
                Category           = r.RelationshipType.Category,
                IsActive           = r.IsActive,
            })
            .ToListAsync();

        // Also pull in related people referenced in relationships but not in the family (e.g. spouses)
        var relatedIds = relationships
            .SelectMany(r => new[] { r.FromPersonId, r.ToPersonId })
            .Distinct()
            .Where(id => !familyIds.Contains(id))
            .ToList();

        var relatedPeople = relatedIds.Count == 0
            ? new List<FamilyPersonDto>()
            : await _db.People
                .Where(p => relatedIds.Contains(p.Id))
                .Select(p => new FamilyPersonDto
                {
                    Id        = p.Id,
                    FullName  = p.FirstName + " " + p.LastName,
                    FirstName = p.FirstName,
                    LastName  = p.LastName,
                    Gender    = p.Gender.ToString().ToLower(),
                })
                .ToListAsync();

        // One extra hop: pull spouse relationships for related people so that spouses-of-children
        // who share a different last name (e.g. Maria Cena as daughter of Ebreos) still appear.
        var spouseTypeIds = new HashSet<int> { 5, 6 };
        if (relatedIds.Count > 0)
        {
            var spouseRels = await _db.PersonRelationships
                .Include(r => r.FromPerson)
                .Include(r => r.ToPerson)
                .Include(r => r.RelationshipType)
                .Where(r => r.IsActive
                         && spouseTypeIds.Contains(r.RelationshipTypeId)
                         && (relatedIds.Contains(r.FromPersonId) || relatedIds.Contains(r.ToPersonId)))
                .Select(r => new PersonRelationshipDto
                {
                    Id                 = r.Id,
                    FromPersonId       = r.FromPersonId,
                    FromPersonName     = r.FromPerson.FirstName + " " + r.FromPerson.LastName,
                    ToPersonId         = r.ToPersonId,
                    ToPersonName       = r.ToPerson.FirstName   + " " + r.ToPerson.LastName,
                    RelationshipTypeId = r.RelationshipTypeId,
                    TypeCode           = r.RelationshipType.TypeCode,
                    TypeLabel          = r.RelationshipType.TypeLabel,
                    Category           = r.RelationshipType.Category,
                    IsActive           = r.IsActive,
                })
                .ToListAsync();

            var existingRelIds = relationships.Select(r => r.Id).ToHashSet();
            var newSpouseRels  = spouseRels.Where(r => !existingRelIds.Contains(r.Id)).ToList();

            var spousePersonIds = newSpouseRels
                .SelectMany(r => new[] { r.FromPersonId, r.ToPersonId })
                .Distinct()
                .Where(id => !familyIds.Contains(id) && !relatedIds.Contains(id))
                .ToList();

            if (spousePersonIds.Count > 0)
            {
                var spousePeople = await _db.People
                    .Where(p => spousePersonIds.Contains(p.Id))
                    .Select(p => new FamilyPersonDto
                    {
                        Id        = p.Id,
                        FullName  = p.FirstName + " " + p.LastName,
                        FirstName = p.FirstName,
                        LastName  = p.LastName,
                        Gender    = p.Gender.ToString().ToLower(),
                    })
                    .ToListAsync();
                relatedPeople = relatedPeople.Concat(spousePeople).ToList();
            }

            relationships = relationships.Concat(newSpouseRels).ToList();
        }

        return new FamilyTreeDataDto
        {
            People        = familyPeople.Concat(relatedPeople).ToList(),
            Relationships = relationships,
        };
    }

    public async Task<List<RelationshipTypeDto>> GetRelationshipTypesAsync() =>
        await _db.RelationshipTypes
            .Where(r => r.IsActive)
            .OrderBy(r => r.Category).ThenBy(r => r.TypeLabel)
            .Select(r => new RelationshipTypeDto
            {
                Id = r.Id, TypeCode = r.TypeCode, TypeLabel = r.TypeLabel,
                Category = r.Category, GenerationDelta = r.GenerationDelta
            })
            .ToListAsync();

    public async Task<List<PersonRelationshipDto>> GetPersonRelationshipsAsync(int personId)
    {
        if (!await _db.People.AnyAsync(p => p.Id == personId))
            throw new KeyNotFoundException($"Person {personId} not found.");

        return await _db.PersonRelationships
            .Include(r => r.FromPerson).Include(r => r.ToPerson).Include(r => r.RelationshipType)
            .Where(r => r.FromPersonId == personId && r.IsActive)
            .OrderBy(r => r.RelationshipType.Category).ThenBy(r => r.RelationshipType.TypeLabel)
            .Select(r => new PersonRelationshipDto
            {
                Id = r.Id,
                FromPersonId = r.FromPersonId, FromPersonName = r.FromPerson.FirstName + " " + r.FromPerson.LastName,
                ToPersonId = r.ToPersonId,     ToPersonName   = r.ToPerson.FirstName   + " " + r.ToPerson.LastName,
                RelationshipTypeId = r.RelationshipTypeId,
                TypeCode = r.RelationshipType.TypeCode, TypeLabel = r.RelationshipType.TypeLabel,
                Category = r.RelationshipType.Category, IsActive = r.IsActive
            })
            .ToListAsync();
    }

    public async Task<PersonRelationshipDto> CreateRelationshipAsync(CreateRelationshipDto dto)
    {
        if (dto.FromPersonId == dto.ToPersonId)
            throw new DomainException("A person cannot have a relationship with themselves.");

        if (!await _db.People.AnyAsync(p => p.Id == dto.FromPersonId))
            throw new KeyNotFoundException($"Person {dto.FromPersonId} not found.");
        if (!await _db.People.AnyAsync(p => p.Id == dto.ToPersonId))
            throw new KeyNotFoundException($"Person {dto.ToPersonId} not found.");
        if (!await _db.RelationshipTypes.AnyAsync(r => r.Id == dto.RelationshipTypeId))
            throw new KeyNotFoundException($"RelationshipType {dto.RelationshipTypeId} not found.");
        if (await _db.PersonRelationships.AnyAsync(r =>
                r.FromPersonId == dto.FromPersonId &&
                r.ToPersonId == dto.ToPersonId &&
                r.RelationshipTypeId == dto.RelationshipTypeId))
            throw new DomainException("This relationship already exists.");

        var rel = new PersonRelationship
        {
            FromPersonId = dto.FromPersonId, ToPersonId = dto.ToPersonId,
            RelationshipTypeId = dto.RelationshipTypeId, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        _db.PersonRelationships.Add(rel);
        await _db.SaveChangesAsync();

        return await _db.PersonRelationships
            .Include(r => r.FromPerson).Include(r => r.ToPerson).Include(r => r.RelationshipType)
            .Where(r => r.Id == rel.Id)
            .Select(r => new PersonRelationshipDto
            {
                Id = r.Id,
                FromPersonId = r.FromPersonId, FromPersonName = r.FromPerson.FirstName + " " + r.FromPerson.LastName,
                ToPersonId = r.ToPersonId,     ToPersonName   = r.ToPerson.FirstName   + " " + r.ToPerson.LastName,
                RelationshipTypeId = r.RelationshipTypeId,
                TypeCode = r.RelationshipType.TypeCode, TypeLabel = r.RelationshipType.TypeLabel,
                Category = r.RelationshipType.Category, IsActive = r.IsActive
            })
            .FirstAsync();
    }

    public async Task DeleteRelationshipAsync(int id)
    {
        var rel = await _db.PersonRelationships.FindAsync(id)
            ?? throw new KeyNotFoundException($"Relationship {id} not found.");
        _db.PersonRelationships.Remove(rel);
        await _db.SaveChangesAsync();
    }

    private static readonly HashSet<string> AllowedRoleCodes = new()
    {
        "FATHER", "MOTHER", "BROTHER", "SISTER",
        "STEP_FATHER", "STEP_MOTHER", "STEP_BROTHER", "STEP_SISTER",
    };

    private static Gender GenderFromRoleCode(string roleCode) => roleCode switch
    {
        "FATHER" or "BROTHER" or "STEP_FATHER" or "STEP_BROTHER" => Gender.Male,
        _ => Gender.Female,
    };

    public async Task<PersonDto> AddFamilyMemberAsync(AddFamilyMemberDto dto)
    {
        if (!AllowedRoleCodes.Contains(dto.RoleCode))
            throw new DomainException($"Invalid roleCode '{dto.RoleCode}'.");

        if (dto.FatherId.HasValue && !await _db.People.AnyAsync(p => p.Id == dto.FatherId.Value))
            throw new KeyNotFoundException($"Father person {dto.FatherId} not found.");
        if (dto.MotherId.HasValue && !await _db.People.AnyAsync(p => p.Id == dto.MotherId.Value))
            throw new KeyNotFoundException($"Mother person {dto.MotherId} not found.");

        var newPerson = await _personService.CreateAsync(new CreatePersonDto
        {
            FirstName = dto.FirstName,
            LastName  = dto.LastName,
            Gender    = GenderFromRoleCode(dto.RoleCode),
        });

        var existingChildren = await GetChildrenOfParentsAsync(dto.FatherId, dto.MotherId);

        var neededCodes = GetNeededTypeCodes(dto.RoleCode, existingChildren);
        var typeIdByCode = await _db.RelationshipTypes
            .Where(rt => neededCodes.Contains(rt.TypeCode) && rt.IsActive)
            .ToDictionaryAsync(rt => rt.TypeCode, rt => rt.Id);

        var relsToCreate = BuildFamilyRelationships(
            newPerson.Id, dto.RoleCode, dto.FatherId, dto.MotherId, existingChildren, typeIdByCode);

        if (relsToCreate.Count > 0)
        {
            var existingSet = await _db.PersonRelationships
                .Where(r => relsToCreate.Select(x => x.FromPersonId).Contains(r.FromPersonId))
                .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
                .ToListAsync();
            var existingKeys = existingSet
                .Select(r => (r.FromPersonId, r.ToPersonId, r.RelationshipTypeId))
                .ToHashSet();

            var deduped = relsToCreate
                .Where(r => !existingKeys.Contains((r.FromPersonId, r.ToPersonId, r.RelationshipTypeId)))
                .ToList();

            if (deduped.Count > 0)
            {
                _db.PersonRelationships.AddRange(deduped);
                await _db.SaveChangesAsync();
            }
        }

        return newPerson;
    }

    private async Task<List<(int Id, Gender Gender)>> GetChildrenOfParentsAsync(int? fatherId, int? motherId)
    {
        if (fatherId == null && motherId == null)
            return new List<(int, Gender)>();

        var parentIds = new List<int>();
        if (fatherId.HasValue) parentIds.Add(fatherId.Value);
        if (motherId.HasValue) parentIds.Add(motherId.Value);

        // FATHER/MOTHER: FROM=parent, TO=child
        var childIdsA = await _db.PersonRelationships
            .Where(r => r.IsActive && (r.RelationshipTypeId == 1 || r.RelationshipTypeId == 2)
                     && parentIds.Contains(r.FromPersonId))
            .Select(r => r.ToPersonId)
            .ToListAsync();

        // SON/DAUGHTER: FROM=child, TO=parent
        var childIdsB = await _db.PersonRelationships
            .Where(r => r.IsActive && (r.RelationshipTypeId == 3 || r.RelationshipTypeId == 4)
                     && parentIds.Contains(r.ToPersonId))
            .Select(r => r.FromPersonId)
            .ToListAsync();

        var allChildIds = childIdsA.Concat(childIdsB).Distinct().ToList();
        if (allChildIds.Count == 0)
            return new List<(int, Gender)>();

        var children = await _db.People
            .Where(p => allChildIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Gender })
            .ToListAsync();

        return children.Select(c => (c.Id, c.Gender)).ToList();
    }

    private static HashSet<string> GetNeededTypeCodes(string roleCode, List<(int Id, Gender Gender)> existingChildren)
    {
        var codes = new HashSet<string>();
        switch (roleCode)
        {
            case "FATHER":
                if (existingChildren.Count > 0) codes.Add("FATHER");
                codes.Add("HUSBAND");
                break;
            case "MOTHER":
                if (existingChildren.Count > 0) codes.Add("MOTHER");
                codes.Add("WIFE");
                break;
            case "BROTHER":
                codes.Add("SON"); codes.Add("BROTHER"); codes.Add("SISTER");
                break;
            case "SISTER":
                codes.Add("DAUGHTER"); codes.Add("SISTER"); codes.Add("BROTHER");
                break;
            case "STEP_FATHER":
                codes.Add("STEP_FATHER");
                break;
            case "STEP_MOTHER":
                codes.Add("STEP_MOTHER");
                break;
            case "STEP_BROTHER":
                codes.Add("STEP_BROTHER"); codes.Add("STEP_SISTER");
                break;
            case "STEP_SISTER":
                codes.Add("STEP_SISTER"); codes.Add("STEP_BROTHER");
                break;
        }
        return codes;
    }

    private static List<PersonRelationship> BuildFamilyRelationships(
        int newPersonId,
        string roleCode,
        int? fatherId,
        int? motherId,
        List<(int Id, Gender Gender)> existingChildren,
        Dictionary<string, int> typeIdByCode)
    {
        var result = new List<PersonRelationship>();
        var parents = new List<int>();
        if (fatherId.HasValue) parents.Add(fatherId.Value);
        if (motherId.HasValue) parents.Add(motherId.Value);

        void Add(int fromId, int toId, string typeCode)
        {
            if (typeIdByCode.TryGetValue(typeCode, out var typeId))
                result.Add(new PersonRelationship
                {
                    FromPersonId = fromId,
                    ToPersonId = toId,
                    RelationshipTypeId = typeId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
        }

        switch (roleCode)
        {
            case "FATHER":
                foreach (var child in existingChildren)
                    Add(newPersonId, child.Id, "FATHER");
                if (motherId.HasValue)
                    Add(newPersonId, motherId.Value, "HUSBAND");
                break;

            case "MOTHER":
                foreach (var child in existingChildren)
                    Add(newPersonId, child.Id, "MOTHER");
                if (fatherId.HasValue)
                    Add(newPersonId, fatherId.Value, "WIFE");
                break;

            case "BROTHER":
                foreach (var parentId in parents)
                    Add(newPersonId, parentId, "SON");
                foreach (var child in existingChildren)
                {
                    Add(newPersonId, child.Id, "BROTHER");
                    Add(child.Id, newPersonId, child.Gender == Gender.Female ? "SISTER" : "BROTHER");
                }
                break;

            case "SISTER":
                foreach (var parentId in parents)
                    Add(newPersonId, parentId, "DAUGHTER");
                foreach (var child in existingChildren)
                {
                    Add(newPersonId, child.Id, "SISTER");
                    Add(child.Id, newPersonId, child.Gender == Gender.Female ? "SISTER" : "BROTHER");
                }
                break;

            case "STEP_FATHER":
                foreach (var child in existingChildren)
                    Add(newPersonId, child.Id, "STEP_FATHER");
                break;

            case "STEP_MOTHER":
                foreach (var child in existingChildren)
                    Add(newPersonId, child.Id, "STEP_MOTHER");
                break;

            case "STEP_BROTHER":
                foreach (var child in existingChildren)
                {
                    Add(newPersonId, child.Id, "STEP_BROTHER");
                    Add(child.Id, newPersonId, child.Gender == Gender.Female ? "STEP_SISTER" : "STEP_BROTHER");
                }
                break;

            case "STEP_SISTER":
                foreach (var child in existingChildren)
                {
                    Add(newPersonId, child.Id, "STEP_SISTER");
                    Add(child.Id, newPersonId, child.Gender == Gender.Female ? "STEP_SISTER" : "STEP_BROTHER");
                }
                break;
        }

        return result;
    }

    // ── AddWeddingRelativeAsync ────────────────────────────────────────────────

    private static readonly HashSet<string> AllowedWeddingRelCodes = new()
    {
        "BROTHER", "SISTER", "STEP_FATHER", "STEP_MOTHER", "STEP_BROTHER", "STEP_SISTER",
        "SON", "DAUGHTER", "STEP_SON", "STEP_DAUGHTER",
    };

    public async Task<PersonDto> AddWeddingRelativeAsync(AddWeddingRelativeDto dto)
    {
        if (!AllowedWeddingRelCodes.Contains(dto.TypeCode))
            throw new DomainException($"Invalid typeCode '{dto.TypeCode}' for AddWeddingRelative.");

        var isMale = dto.TypeCode is "BROTHER" or "STEP_FATHER" or "STEP_BROTHER" or "SON" or "STEP_SON";

        var newPerson = await _personService.CreateAsync(new CreatePersonDto
        {
            FirstName = dto.FirstName,
            LastName  = dto.LastName,
            Gender    = isMale ? Gender.Male : Gender.Female,
        });

        bool? relatedIsMale = null;
        if (dto.TypeCode is "SON" or "DAUGHTER" or "STEP_SON" or "STEP_DAUGHTER")
        {
            var relPerson = await _db.People.FindAsync(dto.RelatedPersonId);
            relatedIsMale = relPerson?.Gender == Gender.Male;
        }

        // Resolve weddingId first so we can auto-derive the spouse
        var weddingId = dto.WeddingId;
        if (weddingId <= 0 && dto.RelatedPersonId > 0)
        {
            weddingId = await _db.WeddingDetails
                .Where(d => d.PersonId == dto.RelatedPersonId)
                .Select(d => d.WeddingId)
                .FirstOrDefaultAsync();
        }

        // Auto-derive spouse from WeddingDetails when not provided by the caller
        int? resolvedSpouseId = dto.SpouseId;
        bool? resolvedSpouseIsMale = dto.SpouseIsMale;

        if (weddingId > 0 && !resolvedSpouseId.HasValue && dto.RelatedPersonId > 0)
        {
            var relatedDetail = await _db.WeddingDetails
                .FirstOrDefaultAsync(d => d.WeddingId == weddingId && d.PersonId == dto.RelatedPersonId);

            if (relatedDetail != null)
            {
                var spouseRole = relatedDetail.RoleType == RoleType.Groom ? RoleType.Bride
                               : relatedDetail.RoleType == RoleType.Bride ? RoleType.Groom
                               : (RoleType?)null;

                if (spouseRole.HasValue)
                {
                    var spouseDetail = await _db.WeddingDetails
                        .Include(d => d.Person)
                        .FirstOrDefaultAsync(d => d.WeddingId == weddingId
                                               && d.RoleType == spouseRole.Value
                                               && d.PersonId.HasValue);
                    if (spouseDetail?.Person != null)
                    {
                        resolvedSpouseId = spouseDetail.PersonId;
                        resolvedSpouseIsMale = spouseDetail.Person.Gender == Gender.Male;
                    }
                }
            }
        }

        var dtoPairs    = BuildWeddingRelativePairs(newPerson.Id, dto, isMale, relatedIsMale, resolvedSpouseId, resolvedSpouseIsMale);
        var dtoPairsSet = new HashSet<(int, int, string)>(dtoPairs);

        var dtoParentIds = new List<int>();
        if (dto.FatherId.HasValue) dtoParentIds.Add(dto.FatherId.Value);
        if (dto.MotherId.HasValue) dtoParentIds.Add(dto.MotherId.Value);

        var dbPairs = await BuildDbDerivedPairsAsync(
            newPerson.Id, dto.RelatedPersonId, dto.TypeCode, isMale, resolvedSpouseId,
            dtoParentIds, dtoPairsSet);

        await BulkUpsertRelationshipsAsync(dtoPairs.Concat(dbPairs).ToList());

        if (weddingId > 0)
        {
            var relType = await _db.RelationshipTypes
                .FirstOrDefaultAsync(rt => rt.TypeCode == dto.TypeCode && rt.IsActive);

            var existing = await _db.WeddingDetails
                .FirstOrDefaultAsync(d => d.WeddingId == weddingId && d.PersonId == newPerson.Id);

            if (existing == null && relType != null)
            {
                _db.WeddingDetails.Add(new WeddingDetail
                {
                    WeddingId = weddingId,
                    PersonId = newPerson.Id,
                    RoleType = RoleType.OtherRelation,
                    InWeddingRelationTypeId = relType.Id
                });
                await _db.SaveChangesAsync();
            }
        }

        return newPerson;
    }

    private static List<(int From, int To, string Code)> BuildWeddingRelativePairs(
        int newId, AddWeddingRelativeDto dto, bool newIsMale, bool? relatedIsMale = null,
        int? spouseIdOverride = null, bool? spouseIsMaleOverride = null)
    {
        var pairs = new List<(int, int, string)>();
        void Add(int from, int to, string code) { if (from != to) pairs.Add((from, to, code)); }

        var spouseId   = spouseIdOverride   ?? dto.SpouseId;
        var spouseIsMale = spouseIsMaleOverride ?? dto.SpouseIsMale;

        // 1. Direct relationship
        Add(newId, dto.RelatedPersonId, dto.TypeCode);

        // 2. Inverse relationship
        var inv = dto.TypeCode switch
        {
            "BROTHER"      => newIsMale ? "BROTHER"      : "SISTER",
            "SISTER"       => newIsMale ? "BROTHER"      : "SISTER",
            "STEP_FATHER"  => newIsMale ? "STEP_SON"     : "STEP_DAUGHTER",
            "STEP_MOTHER"  => newIsMale ? "STEP_SON"     : "STEP_DAUGHTER",
            "STEP_BROTHER" => newIsMale ? "STEP_BROTHER" : "STEP_SISTER",
            "STEP_SISTER"  => newIsMale ? "STEP_BROTHER" : "STEP_SISTER",
            "SON"      or "DAUGHTER"      when relatedIsMale.HasValue => relatedIsMale.Value ? "FATHER"      : "MOTHER",
            "STEP_SON" or "STEP_DAUGHTER" when relatedIsMale.HasValue => relatedIsMale.Value ? "STEP_FATHER" : "STEP_MOTHER",
            _              => (string?)null,
        };
        if (inv != null) Add(dto.RelatedPersonId, newId, inv);

        // 3a. Child types: link new person to the related person's spouse as the other parent
        bool isChildType = dto.TypeCode is "SON" or "DAUGHTER" or "STEP_SON" or "STEP_DAUGHTER";
        bool isStepChild = dto.TypeCode is "STEP_SON" or "STEP_DAUGHTER";

        if (isChildType && spouseId.HasValue && spouseIsMale.HasValue)
        {
            var spouseParentCode = spouseIsMale.Value
                ? (isStepChild ? "STEP_FATHER" : "FATHER")
                : (isStepChild ? "STEP_MOTHER" : "MOTHER");
            var childCode = newIsMale
                ? (isStepChild ? "STEP_SON"      : "SON")
                : (isStepChild ? "STEP_DAUGHTER" : "DAUGHTER");
            Add(newId, spouseId.Value, childCode);
            Add(spouseId.Value, newId, spouseParentCode);
        }

        // 3b. Blood-sibling: link to parents and grandparents on the correct side
        if (dto.TypeCode is "BROTHER" or "SISTER")
        {
            var childCode = newIsMale ? "SON" : "DAUGHTER";
            var gcCode    = newIsMale ? "GRANDSON" : "GRANDDAUGHTER";

            if (dto.FatherId.HasValue)
            {
                Add(newId, dto.FatherId.Value, childCode);
                Add(dto.FatherId.Value, newId, "FATHER");

                if (dto.PaternalGrandfatherId.HasValue)
                {
                    Add(newId, dto.PaternalGrandfatherId.Value, gcCode);
                    Add(dto.PaternalGrandfatherId.Value, newId, "GRANDFATHER");
                }
                if (dto.PaternalGrandmotherId.HasValue)
                {
                    Add(newId, dto.PaternalGrandmotherId.Value, gcCode);
                    Add(dto.PaternalGrandmotherId.Value, newId, "GRANDMOTHER");
                }
            }

            if (dto.MotherId.HasValue)
            {
                Add(newId, dto.MotherId.Value, childCode);
                Add(dto.MotherId.Value, newId, "MOTHER");

                if (dto.MaternalGrandfatherId.HasValue)
                {
                    Add(newId, dto.MaternalGrandfatherId.Value, gcCode);
                    Add(dto.MaternalGrandfatherId.Value, newId, "GRANDFATHER");
                }
                if (dto.MaternalGrandmotherId.HasValue)
                {
                    Add(newId, dto.MaternalGrandmotherId.Value, gcCode);
                    Add(dto.MaternalGrandmotherId.Value, newId, "GRANDMOTHER");
                }
            }

            // 4. Spouse-side in-law (Brother-in-Law / Sister-in-Law)
            if (spouseId.HasValue)
            {
                var newPersonInLaw = newIsMale ? "BROTHER_IN_LAW" : "SISTER_IN_LAW";
                var spouseInLaw    = (spouseIsMale == true) ? "BROTHER_IN_LAW" : "SISTER_IN_LAW";
                Add(newId, spouseId.Value, newPersonInLaw);
                Add(spouseId.Value, newId, spouseInLaw);
            }
        }

        return pairs;
    }

    private async Task<List<(int From, int To, string Code)>> BuildDbDerivedPairsAsync(
        int newId,
        int relatedId,
        string typeCode,
        bool newIsMale,
        int? resolvedSpouseId,
        IReadOnlyCollection<int> dtoParentIds,
        HashSet<(int, int, string)> existingPairsSet)
    {
        var result = new List<(int, int, string)>();
        var seen   = new HashSet<(int, int, string)>(existingPairsSet);

        void Add(int from, int to, string code)
        {
            if (from == to) return;
            if (!seen.Add((from, to, code))) return;
            result.Add((from, to, code));
        }

        // Resolve TypeCode → TypeId once for all filter sets
        var typeIdByCode = await _db.RelationshipTypes
            .Where(rt => rt.IsActive)
            .ToDictionaryAsync(rt => rt.TypeCode, rt => rt.Id);

        var typeCodeById = typeIdByCode.ToDictionary(kv => kv.Value, kv => kv.Key);

        int TId(string code) => typeIdByCode.GetValueOrDefault(code, 0);

        var parentTypeIds     = new HashSet<int> { TId("FATHER"),      TId("MOTHER"),      TId("STEP_FATHER"),  TId("STEP_MOTHER")  };
        var childTypeIds      = new HashSet<int> { TId("SON"),          TId("DAUGHTER"),    TId("STEP_SON"),     TId("STEP_DAUGHTER") };
        var stepChildTypeIds  = new HashSet<int> { TId("STEP_SON"),     TId("STEP_DAUGHTER") };
        var stepParentTypeIds = new HashSet<int> { TId("STEP_FATHER"),  TId("STEP_MOTHER")   };
        var spouseTypeIds     = new HashSet<int> { TId("HUSBAND"),      TId("WIFE")          };
        var siblingTypeIds    = new HashSet<int> { TId("BROTHER"),      TId("SISTER")        };

        foreach (var s in new[] { parentTypeIds, childTypeIds, stepChildTypeIds, stepParentTypeIds, spouseTypeIds, siblingTypeIds })
            s.Remove(0);

        bool IsStepTypeId(int id) => typeCodeById.TryGetValue(id, out var c) &&
                                     c is "STEP_FATHER" or "STEP_MOTHER" or "STEP_SON" or "STEP_DAUGHTER";

        // ── BROTHER / SISTER ─────────────────────────────────────────────────
        if (typeCode is "BROTHER" or "SISTER")
        {
            var childCode = newIsMale ? "SON" : "DAUGHTER";
            var gcCode    = newIsMale ? "GRANDSON" : "GRANDDAUGHTER";

            // 1. Parents of relatedId from DB
            var parentRawRows = await _db.PersonRelationships
                .Where(r => r.IsActive && (
                    (r.FromPersonId == relatedId && childTypeIds.Contains(r.RelationshipTypeId)) ||
                    (r.ToPersonId   == relatedId && parentTypeIds.Contains(r.RelationshipTypeId))
                ))
                .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
                .ToListAsync();

            var dbParentIds = new HashSet<int>();
            foreach (var row in parentRawRows)
            {
                var parentId = row.FromPersonId == relatedId ? row.ToPersonId : row.FromPersonId;
                dbParentIds.Add(parentId);
            }

            var allParentIds = dbParentIds.Union(dtoParentIds).Where(id => id > 0).ToList();

            var parentGenders = allParentIds.Count > 0
                ? await _db.People.Where(p => allParentIds.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                : new Dictionary<int, Gender>();

            foreach (var row in parentRawRows)
            {
                var parentId     = row.FromPersonId == relatedId ? row.ToPersonId : row.FromPersonId;
                var pg           = parentGenders.GetValueOrDefault(parentId, Gender.Unknown);
                bool isStep      = IsStepTypeId(row.RelationshipTypeId);
                var newChildCode = isStep ? (newIsMale ? "STEP_SON" : "STEP_DAUGHTER") : childCode;
                var parentCode   = isStep ? (pg == Gender.Female ? "STEP_MOTHER" : "STEP_FATHER")
                                          : (pg == Gender.Female ? "MOTHER"      : "FATHER");
                Add(newId, parentId, newChildCode);
                Add(parentId, newId, parentCode);
            }

            // 2. Grandparents (parents of all parents via PersonRelationships only)
            if (allParentIds.Count > 0)
            {
                var allParentSet = allParentIds.ToHashSet();
                var gpRawRows = await _db.PersonRelationships
                    .Where(r => r.IsActive && (
                        (allParentSet.Contains(r.FromPersonId) && childTypeIds.Contains(r.RelationshipTypeId)) ||
                        (allParentSet.Contains(r.ToPersonId)   && parentTypeIds.Contains(r.RelationshipTypeId))
                    ))
                    .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
                    .ToListAsync();

                var gpIds = gpRawRows
                    .Select(r => allParentSet.Contains(r.FromPersonId) ? r.ToPersonId : r.FromPersonId)
                    .Where(id => id != relatedId && id != newId)
                    .Distinct().ToList();

                var gpGenders = gpIds.Count > 0
                    ? await _db.People.Where(p => gpIds.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                    : new Dictionary<int, Gender>();

                var seenGpIds = new HashSet<int>();
                foreach (var gpRow in gpRawRows)
                {
                    var gpId = allParentSet.Contains(gpRow.FromPersonId) ? gpRow.ToPersonId : gpRow.FromPersonId;
                    if (gpId == relatedId || gpId == newId) continue;
                    if (!seenGpIds.Add(gpId)) continue;

                    var gpGender  = gpGenders.GetValueOrDefault(gpId, Gender.Unknown);
                    bool isStep   = IsStepTypeId(gpRow.RelationshipTypeId);
                    var gpCode    = isStep ? (gpGender == Gender.Female ? "STEP_GRANDMOTHER" : "STEP_GRANDFATHER")
                                          : (gpGender == Gender.Female ? "GRANDMOTHER"      : "GRANDFATHER");
                    var newGcCode = isStep ? (newIsMale ? "STEP_GRANDSON" : "STEP_GRANDDAUGHTER") : gcCode;

                    Add(newId, gpId, newGcCode);
                    Add(gpId, newId, gpCode);
                }
            }

            // 3. Step-children of all parents (DTO + DB) → step-siblings of newPerson
            if (allParentIds.Count > 0)
            {
                var allParentSet = allParentIds.ToHashSet();
                var scRawRows = await _db.PersonRelationships
                    .Where(r => r.IsActive && (
                        (allParentSet.Contains(r.ToPersonId)   && stepChildTypeIds.Contains(r.RelationshipTypeId)) ||
                        (allParentSet.Contains(r.FromPersonId) && stepParentTypeIds.Contains(r.RelationshipTypeId))
                    ))
                    .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
                    .ToListAsync();

                var stepChildIds = scRawRows
                    .Select(r => allParentSet.Contains(r.ToPersonId) ? r.FromPersonId : r.ToPersonId)
                    .Where(id => id != relatedId && id != newId)
                    .Distinct()
                    .ToList();

                var scGenders = stepChildIds.Count > 0
                    ? await _db.People.Where(p => stepChildIds.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                    : new Dictionary<int, Gender>();

                foreach (var stepChildId in stepChildIds)
                {
                    var scGender = scGenders.GetValueOrDefault(stepChildId, Gender.Unknown);
                    Add(newId, stepChildId, newIsMale ? "STEP_BROTHER" : "STEP_SISTER");
                    Add(stepChildId, newId, scGender == Gender.Female ? "STEP_SISTER" : "STEP_BROTHER");
                }
            }

            // 4. Existing siblings of relatedId → also siblings of newPerson
            var sibRawRows = await _db.PersonRelationships
                .Where(r => r.IsActive && siblingTypeIds.Contains(r.RelationshipTypeId) && (
                    r.FromPersonId == relatedId || r.ToPersonId == relatedId
                ))
                .Select(r => new { r.FromPersonId, r.ToPersonId })
                .ToListAsync();

            var sibIds = sibRawRows
                .Select(r => r.FromPersonId == relatedId ? r.ToPersonId : r.FromPersonId)
                .Where(id => id != newId)
                .Distinct()
                .ToList();

            var sibGenders = sibIds.Count > 0
                ? await _db.People.Where(p => sibIds.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                : new Dictionary<int, Gender>();

            foreach (var sibId in sibIds)
            {
                var sibGender = sibGenders.GetValueOrDefault(sibId, Gender.Unknown);
                Add(newId, sibId,  newIsMale ? "BROTHER" : "SISTER");
                Add(sibId,  newId, sibGender == Gender.Female ? "SISTER" : "BROTHER");
            }

            // 4b. Spouse of each sibling → in-law relationships for newPerson
            if (sibIds.Count > 0)
            {
                var sibSpouseRows = await _db.PersonRelationships
                    .Where(r => r.IsActive && spouseTypeIds.Contains(r.RelationshipTypeId) && (
                        sibIds.Contains(r.FromPersonId) || sibIds.Contains(r.ToPersonId)
                    ))
                    .Select(r => new { r.FromPersonId, r.ToPersonId })
                    .ToListAsync();

                var sibSpouseIds = sibSpouseRows
                    .Select(r => sibIds.Contains(r.FromPersonId) ? r.ToPersonId : r.FromPersonId)
                    .Where(id => id != newId && id != relatedId)
                    .Distinct().ToList();

                var sibSpouseGenders = sibSpouseIds.Count > 0
                    ? await _db.People.Where(p => sibSpouseIds.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                    : new Dictionary<int, Gender>();

                foreach (var sibSpouseId in sibSpouseIds)
                {
                    var ssg = sibSpouseGenders.GetValueOrDefault(sibSpouseId, Gender.Unknown);
                    Add(newId, sibSpouseId, newIsMale ? "BROTHER_IN_LAW" : "SISTER_IN_LAW");
                    Add(sibSpouseId, newId, ssg == Gender.Female ? "SISTER_IN_LAW" : "BROTHER_IN_LAW");
                }
            }

            // 5. Spouse of relatedId — check PersonRelationships then WeddingDetails
            if (!resolvedSpouseId.HasValue)
            {
                int? derivedSpouseId = null;

                // 5a. PersonRelationships (HUSBAND / WIFE row)
                var spouseRelRow = await _db.PersonRelationships
                    .Where(r => r.IsActive && spouseTypeIds.Contains(r.RelationshipTypeId) && (
                        r.FromPersonId == relatedId || r.ToPersonId == relatedId
                    ))
                    .Select(r => new { SpouseId = r.FromPersonId == relatedId ? r.ToPersonId : r.FromPersonId })
                    .FirstOrDefaultAsync();
                if (spouseRelRow != null) derivedSpouseId = spouseRelRow.SpouseId;

                // 5b. WeddingDetails (Groom ↔ Bride link, e.g. when spouse is only linked via the wedding)
                if (!derivedSpouseId.HasValue)
                {
                    var wedDetail = await _db.WeddingDetails
                        .Where(d => d.PersonId == relatedId &&
                                    (d.RoleType == RoleType.Groom || d.RoleType == RoleType.Bride))
                        .Select(d => new { d.WeddingId, d.RoleType })
                        .FirstOrDefaultAsync();

                    if (wedDetail != null)
                    {
                        var oppRole = wedDetail.RoleType == RoleType.Groom ? RoleType.Bride : RoleType.Groom;
                        derivedSpouseId = await _db.WeddingDetails
                            .Where(d => d.WeddingId == wedDetail.WeddingId &&
                                        d.RoleType == oppRole && d.PersonId.HasValue)
                            .Select(d => d.PersonId)
                            .FirstOrDefaultAsync();
                    }
                }

                if (derivedSpouseId.HasValue && derivedSpouseId.Value != newId)
                {
                    var spouseGender = await _db.People
                        .Where(p => p.Id == derivedSpouseId.Value)
                        .Select(p => p.Gender)
                        .FirstOrDefaultAsync();

                    Add(newId, derivedSpouseId.Value, newIsMale ? "BROTHER_IN_LAW" : "SISTER_IN_LAW");
                    Add(derivedSpouseId.Value, newId, spouseGender == Gender.Female ? "SISTER_IN_LAW" : "BROTHER_IN_LAW");
                }
            }
        }

        // ── SON / DAUGHTER / STEP_SON / STEP_DAUGHTER ────────────────────────
        else if (typeCode is "SON" or "DAUGHTER" or "STEP_SON" or "STEP_DAUGHTER")
        {
            bool isStepChild = typeCode is "STEP_SON" or "STEP_DAUGHTER";
            var newChildCode2 = isStepChild ? (newIsMale ? "STEP_SON" : "STEP_DAUGHTER") : (newIsMale ? "SON" : "DAUGHTER");

            // L5: co-parent (spouse of relatedId)
            int? coParentId = resolvedSpouseId;
            if (!coParentId.HasValue)
            {
                var spRow = await _db.PersonRelationships
                    .Where(r => r.IsActive && spouseTypeIds.Contains(r.RelationshipTypeId) &&
                                (r.FromPersonId == relatedId || r.ToPersonId == relatedId))
                    .Select(r => new { SpouseId = r.FromPersonId == relatedId ? r.ToPersonId : r.FromPersonId })
                    .FirstOrDefaultAsync();
                if (spRow != null) coParentId = spRow.SpouseId;

                if (!coParentId.HasValue)
                {
                    var wd = await _db.WeddingDetails
                        .Where(d => d.PersonId == relatedId && (d.RoleType == RoleType.Groom || d.RoleType == RoleType.Bride))
                        .Select(d => new { d.WeddingId, d.RoleType }).FirstOrDefaultAsync();
                    if (wd != null)
                    {
                        var opp = wd.RoleType == RoleType.Groom ? RoleType.Bride : RoleType.Groom;
                        coParentId = await _db.WeddingDetails
                            .Where(d => d.WeddingId == wd.WeddingId && d.RoleType == opp && d.PersonId.HasValue)
                            .Select(d => d.PersonId).FirstOrDefaultAsync();
                    }
                }
            }
            if (coParentId.HasValue && coParentId.Value != newId)
            {
                var cpGender = await _db.People.Where(p => p.Id == coParentId.Value).Select(p => p.Gender).FirstOrDefaultAsync();
                var cpCode   = isStepChild ? (cpGender == Gender.Female ? "STEP_MOTHER" : "STEP_FATHER")
                                           : (cpGender == Gender.Female ? "MOTHER"      : "FATHER");
                Add(newId, coParentId.Value, newChildCode2);
                Add(coParentId.Value, newId, cpCode);
            }

            // L1: grandparents (parents of relatedId → grandparents of newId)
            var gpParentRows = await _db.PersonRelationships
                .Where(r => r.IsActive && (
                    (r.FromPersonId == relatedId && childTypeIds.Contains(r.RelationshipTypeId)) ||
                    (r.ToPersonId   == relatedId && parentTypeIds.Contains(r.RelationshipTypeId))
                ))
                .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
                .ToListAsync();

            var gpParentIds = gpParentRows.Select(r => r.FromPersonId == relatedId ? r.ToPersonId : r.FromPersonId).Distinct().ToList();
            var gpParentGenders = gpParentIds.Count > 0
                ? await _db.People.Where(p => gpParentIds.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                : new Dictionary<int, Gender>();

            var gcCode2 = newIsMale ? "GRANDSON" : "GRANDDAUGHTER";
            foreach (var gpRow in gpParentRows)
            {
                var gpId     = gpRow.FromPersonId == relatedId ? gpRow.ToPersonId : gpRow.FromPersonId;
                var gpGender = gpParentGenders.GetValueOrDefault(gpId, Gender.Unknown);
                bool isStepGp = IsStepTypeId(gpRow.RelationshipTypeId);
                var gpCode    = isStepGp ? (gpGender == Gender.Female ? "STEP_GRANDMOTHER" : "STEP_GRANDFATHER")
                                         : (gpGender == Gender.Female ? "GRANDMOTHER"      : "GRANDFATHER");
                var newGcCode = isStepGp ? (newIsMale ? "STEP_GRANDSON" : "STEP_GRANDDAUGHTER") : gcCode2;
                Add(newId, gpId, newGcCode);
                Add(gpId, newId, gpCode);
            }

            // Existing children of relatedId → siblings of newId
            var directChildTypeIds = new HashSet<int> { TId("SON"), TId("DAUGHTER") };
            directChildTypeIds.Remove(0);
            var existingChildRows = await _db.PersonRelationships
                .Where(r => r.IsActive && (
                    (r.FromPersonId == relatedId && directChildTypeIds.Contains(r.RelationshipTypeId)) ||
                    (r.ToPersonId   == relatedId && directChildTypeIds.Contains(r.RelationshipTypeId))
                ))
                .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
                .ToListAsync();

            // Rows where relatedId is the parent (FROM→SON/DAUGHTER→child) — TO is the child
            var existingChildIds = existingChildRows
                .Where(r => r.FromPersonId == relatedId)
                .Select(r => r.ToPersonId)
                .Where(id => id != newId)
                .Distinct().ToList();

            var existingChildGenders = existingChildIds.Count > 0
                ? await _db.People.Where(p => existingChildIds.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                : new Dictionary<int, Gender>();

            foreach (var childId in existingChildIds)
            {
                var cg = existingChildGenders.GetValueOrDefault(childId, Gender.Unknown);
                Add(newId, childId, newIsMale ? "BROTHER" : "SISTER");
                Add(childId, newId, cg == Gender.Female ? "SISTER" : "BROTHER");
            }
        }

        // ── FATHER / MOTHER ──────────────────────────────────────────────────
        else if (typeCode is "FATHER" or "MOTHER")
        {
            // Existing children of relatedId → newId is also their parent
            var directChildTypeIds = new HashSet<int> { TId("SON"), TId("DAUGHTER") };
            directChildTypeIds.Remove(0);
            var childRows = await _db.PersonRelationships
                .Where(r => r.IsActive && directChildTypeIds.Contains(r.RelationshipTypeId) &&
                            (r.FromPersonId == relatedId || r.ToPersonId == relatedId))
                .Select(r => new { r.FromPersonId, r.ToPersonId })
                .ToListAsync();

            var childIds = childRows
                .Select(r => r.FromPersonId == relatedId ? r.ToPersonId : r.FromPersonId)
                .Where(id => id != newId).Distinct().ToList();

            var childGenders = childIds.Count > 0
                ? await _db.People.Where(p => childIds.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                : new Dictionary<int, Gender>();

            var newParentCode = newIsMale ? "FATHER" : "MOTHER";
            foreach (var childId in childIds)
            {
                var cg = childGenders.GetValueOrDefault(childId, Gender.Unknown);
                Add(newId, childId, newParentCode);
                Add(childId, newId, cg == Gender.Female ? "DAUGHTER" : "SON");
            }

            // Spouse of relatedId → co-parent (create HUSBAND/WIFE if not already present)
            if (!resolvedSpouseId.HasValue)
            {
                int? spouseId = null;
                var spRow = await _db.PersonRelationships
                    .Where(r => r.IsActive && spouseTypeIds.Contains(r.RelationshipTypeId) &&
                                (r.FromPersonId == relatedId || r.ToPersonId == relatedId))
                    .Select(r => new { SpouseId = r.FromPersonId == relatedId ? r.ToPersonId : r.FromPersonId })
                    .FirstOrDefaultAsync();
                if (spRow != null) spouseId = spRow.SpouseId;

                if (spouseId.HasValue && spouseId.Value != newId)
                {
                    var spGender = await _db.People.Where(p => p.Id == spouseId.Value).Select(p => p.Gender).FirstOrDefaultAsync();
                    Add(newId, spouseId.Value, newIsMale ? "HUSBAND" : "WIFE");
                    Add(spouseId.Value, newId, spGender == Gender.Female ? "WIFE" : "HUSBAND");
                }
            }
        }

        // ── HUSBAND / WIFE ───────────────────────────────────────────────────
        else if (typeCode is "HUSBAND" or "WIFE")
        {
            // L1: parents of relatedId → in-laws of newId
            var inLawParentRows = await _db.PersonRelationships
                .Where(r => r.IsActive && (
                    (r.FromPersonId == relatedId && childTypeIds.Contains(r.RelationshipTypeId)) ||
                    (r.ToPersonId   == relatedId && parentTypeIds.Contains(r.RelationshipTypeId))
                ))
                .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
                .ToListAsync();

            var inLawParentIds = inLawParentRows.Select(r => r.FromPersonId == relatedId ? r.ToPersonId : r.FromPersonId).Distinct().ToList();
            var inLawParentGenders = inLawParentIds.Count > 0
                ? await _db.People.Where(p => inLawParentIds.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                : new Dictionary<int, Gender>();

            foreach (var ilRow in inLawParentRows)
            {
                var ilId     = ilRow.FromPersonId == relatedId ? ilRow.ToPersonId : ilRow.FromPersonId;
                var ilGender = inLawParentGenders.GetValueOrDefault(ilId, Gender.Unknown);
                bool isStep  = IsStepTypeId(ilRow.RelationshipTypeId);
                var ilCode   = isStep ? (ilGender == Gender.Female ? "STEP_MOTHER_IN_LAW" : "STEP_FATHER_IN_LAW")
                                      : (ilGender == Gender.Female ? "MOTHER_IN_LAW"      : "FATHER_IN_LAW");
                var newCode  = isStep ? (newIsMale ? "STEP_SON_IN_LAW" : "STEP_DAUGHTER_IN_LAW")
                                      : (newIsMale ? "SON_IN_LAW"      : "DAUGHTER_IN_LAW");
                if (TId(ilCode) != 0 && TId(newCode) != 0)
                {
                    Add(newId, ilId, newCode);
                    Add(ilId, newId, ilCode);
                }
            }

            // L3: siblings of relatedId → brothers/sisters-in-law of newId
            var sibRowsW = await _db.PersonRelationships
                .Where(r => r.IsActive && siblingTypeIds.Contains(r.RelationshipTypeId) &&
                            (r.FromPersonId == relatedId || r.ToPersonId == relatedId))
                .Select(r => new { r.FromPersonId, r.ToPersonId })
                .ToListAsync();

            var sibIdsW = sibRowsW.Select(r => r.FromPersonId == relatedId ? r.ToPersonId : r.FromPersonId)
                .Where(id => id != newId).Distinct().ToList();

            var sibGendersW = sibIdsW.Count > 0
                ? await _db.People.Where(p => sibIdsW.Contains(p.Id)).Select(p => new { p.Id, p.Gender }).ToDictionaryAsync(p => p.Id, p => p.Gender)
                : new Dictionary<int, Gender>();

            foreach (var sibId in sibIdsW)
            {
                var sg = sibGendersW.GetValueOrDefault(sibId, Gender.Unknown);
                Add(newId, sibId, newIsMale ? "BROTHER_IN_LAW" : "SISTER_IN_LAW");
                Add(sibId, newId, sg == Gender.Female ? "SISTER_IN_LAW" : "BROTHER_IN_LAW");
            }
        }

        return result;
    }

    public async Task BuildRelationshipsForExistingPersonAsync(
        int personId, int relatedPersonId, string typeCode, int weddingId)
    {
        if (!AllowedWeddingRelCodes.Contains(typeCode)) return;

        var isMale = typeCode is "BROTHER" or "STEP_FATHER" or "STEP_BROTHER" or "SON" or "STEP_SON";

        bool? relatedIsMale = null;
        if (typeCode is "SON" or "DAUGHTER" or "STEP_SON" or "STEP_DAUGHTER")
        {
            var relPerson = await _db.People.FindAsync(relatedPersonId);
            relatedIsMale = relPerson?.Gender == Gender.Male;
        }

        // Derive spouse from WeddingDetails so sibling in-law links are created
        int? resolvedSpouseId = null;
        bool? resolvedSpouseIsMale = null;

        if (weddingId > 0)
        {
            var relatedDetail = await _db.WeddingDetails
                .FirstOrDefaultAsync(d => d.WeddingId == weddingId && d.PersonId == relatedPersonId);

            if (relatedDetail != null)
            {
                var spouseRole = relatedDetail.RoleType == RoleType.Groom ? RoleType.Bride
                               : relatedDetail.RoleType == RoleType.Bride ? RoleType.Groom
                               : (RoleType?)null;

                if (spouseRole.HasValue)
                {
                    var spouseDetail = await _db.WeddingDetails
                        .Include(d => d.Person)
                        .FirstOrDefaultAsync(d => d.WeddingId == weddingId
                                               && d.RoleType == spouseRole.Value
                                               && d.PersonId.HasValue);
                    if (spouseDetail?.Person != null)
                    {
                        resolvedSpouseId = spouseDetail.PersonId;
                        resolvedSpouseIsMale = spouseDetail.Person.Gender == Gender.Male;
                    }
                }
            }
        }

        var dto = new AddWeddingRelativeDto
        {
            TypeCode        = typeCode,
            RelatedPersonId = relatedPersonId,
            WeddingId       = weddingId,
        };

        var dtoPairs    = BuildWeddingRelativePairs(personId, dto, isMale, relatedIsMale, resolvedSpouseId, resolvedSpouseIsMale);
        var dtoPairsSet = new HashSet<(int, int, string)>(dtoPairs);

        var dbPairs = await BuildDbDerivedPairsAsync(
            personId, relatedPersonId, typeCode, isMale, resolvedSpouseId,
            [], dtoPairsSet);

        await BulkUpsertRelationshipsAsync([.. dtoPairs, .. dbPairs]);
    }

    private async Task BulkUpsertRelationshipsAsync(List<(int From, int To, string Code)> pairs)
    {
        if (pairs.Count == 0) return;

        var codes = pairs.Select(p => p.Code).Distinct().ToList();
        var typeIdByCode = await _db.RelationshipTypes
            .Where(rt => codes.Contains(rt.TypeCode) && rt.IsActive)
            .ToDictionaryAsync(rt => rt.TypeCode, rt => rt.Id);

        var candidates = pairs
            .Select(p => typeIdByCode.TryGetValue(p.Code, out var tid)
                ? new PersonRelationship
                {
                    FromPersonId       = p.From,
                    ToPersonId         = p.To,
                    RelationshipTypeId = tid,
                    IsActive           = true,
                    CreatedAt          = DateTime.UtcNow,
                }
                : null)
            .Where(r => r != null)
            .Select(r => r!)
            .ToList();

        if (candidates.Count == 0) return;

        var fromIds = candidates.Select(r => r.FromPersonId).ToHashSet();
        var existing = await _db.PersonRelationships
            .Where(r => fromIds.Contains(r.FromPersonId))
            .Select(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId })
            .ToListAsync();
        var existingKeys = existing
            .Select(r => (r.FromPersonId, r.ToPersonId, r.RelationshipTypeId))
            .ToHashSet();

        var toAdd = candidates
            .Where(r => !existingKeys.Contains((r.FromPersonId, r.ToPersonId, r.RelationshipTypeId)))
            .ToList();

        if (toAdd.Count > 0)
        {
            _db.PersonRelationships.AddRange(toAdd);
            await _db.SaveChangesAsync();
        }
    }
}
