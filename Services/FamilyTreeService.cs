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
                FatherId  = p.FatherId,
                MotherId  = p.MotherId,
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
                    FatherId  = p.FatherId,
                    MotherId  = p.MotherId,
                })
                .ToListAsync();

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
