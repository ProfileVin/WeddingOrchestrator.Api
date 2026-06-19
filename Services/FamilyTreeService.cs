using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.FamilyTree;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models;
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
            .Where(p => p.LastName == "Smith")
            .ToListAsync();

        var totalPeople = allPeople.Count;

        // Group case-insensitively; display each family name in Pascal Case
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

        var spouseTypeIds   = new HashSet<int> { 5, 6 };                         // HUSBAND, WIFE
        var downwardTypeIds = new HashSet<int> { 3, 4, 20, 21, 26, 27, 32, 33 }; // SON, DAUGHTER, SON_IN_LAW, DAUGHTER_IN_LAW, STEP_SON, STEP_DAUGHTER, ADOPTED_SON, ADOPTED_DAUGHTER
        var upwardTypeIds   = new HashSet<int> { 1, 2, 24, 25, 30, 31 };          // FATHER, MOTHER, STEP_FATHER, STEP_MOTHER, ADOPTIVE_FATHER, ADOPTIVE_MOTHER

        var families = byLastName.Select(family =>
            {
                var familyIds = family.Ids;

                // Step 1 — find the root generation (grandparents or parents).
                // A root is any family member that nobody has declared as their child via a
                // downward relationship type (SON, DAUGHTER, etc.). We intentionally avoid
                // checking upward types (FATHER, MOTHER) because those can be stored in either
                // direction depending on how the user entered the data, which caused grandparents
                // to be incorrectly excluded from roots.
                var isDeclaredChildByAnyone = allRels
                    .Where(r => downwardTypeIds.Contains(r.RelationshipTypeId) && familyIds.Contains(r.ToPersonId))
                    .Select(r => r.ToPersonId)
                    .ToHashSet();

                var rootIds = familyIds
                    .Where(id => !isDeclaredChildByAnyone.Contains(id))
                    .ToHashSet();

                if (!rootIds.Any()) rootIds = new HashSet<int>(familyIds);

                // Step 2 — BFS: traverse only same-family people (those in familyIds).
                // People outside the family (married-in spouses, children who took a different
                // last name) are COUNTED but NOT traversed, preventing cross-family inflation.
                // e.g. Axe BFS finds Kurenai Smith (daughter, non-Axe name) → counts her +
                // her spouse Saiyan Smith, then stops. Smith BFS finds Kurenai Smith as a
                // same-name spouse → traverses her and discovers Goku Smith + Chichi Pipi.
                var counted = new HashSet<int>(rootIds);
                var queue   = new Queue<int>(rootIds);

                while (queue.Count > 0)
                {
                    var pid = queue.Dequeue();

                    // Spouses: count always; traverse only if they share the family's last name
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

                    // Children via downward type: FROM=pid says "TO is my son/daughter/…"
                    var childrenA = allRels
                        .Where(r => downwardTypeIds.Contains(r.RelationshipTypeId) && r.FromPersonId == pid)
                        .Select(r => r.ToPersonId);

                    // Children via upward type: FROM=child says "pid is my father/mother/…"
                    var childrenB = allRels
                        .Where(r => upwardTypeIds.Contains(r.RelationshipTypeId) && r.ToPersonId == pid)
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
                            // Child who took a different last name (married out): count but stop
                            // traversing; still pull in their spouse since we stop here
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
