using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.FamilyTree;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class FamilyTreeService : IFamilyTreeService
{
    private readonly AppDbContext _db;
    public FamilyTreeService(AppDbContext db) => _db = db;

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

        var weddingRoles = await _db.WeddingRoles
            .Where(wr => wr.PersonId != null)
            .Select(wr => new { wr.WeddingId, wr.RoleType, PersonId = wr.PersonId!.Value })
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
                Id             = r.Id,
                FromPersonId   = r.FromPersonId,
                FromPersonName = r.FromPerson.FirstName + " " + r.FromPerson.LastName,
                ToPersonId     = r.ToPersonId,
                ToPersonName   = r.ToPerson.FirstName   + " " + r.ToPerson.LastName,
                TypeCode       = r.RelationshipType.TypeCode,
                TypeLabel      = r.RelationshipType.TypeLabel,
                Category       = r.RelationshipType.Category,
                IsActive       = r.IsActive,
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
                    Id             = r.Id,
                    FromPersonId   = r.FromPersonId,
                    FromPersonName = r.FromPerson.FirstName + " " + r.FromPerson.LastName,
                    ToPersonId     = r.ToPersonId,
                    ToPersonName   = r.ToPerson.FirstName   + " " + r.ToPerson.LastName,
                    TypeCode       = r.RelationshipType.TypeCode,
                    TypeLabel      = r.RelationshipType.TypeLabel,
                    Category       = r.RelationshipType.Category,
                    IsActive       = r.IsActive,
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
}
