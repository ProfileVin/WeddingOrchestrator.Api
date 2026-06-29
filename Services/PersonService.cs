using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.People;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class PersonService : IPersonService
{
    private readonly AppDbContext _db;

    public PersonService(AppDbContext db) => _db = db;

    public async Task<List<PersonDto>> GetAllAsync()
    {
        var people = await _db.People
            .Include(p => p.Father)
            .Include(p => p.Mother)
            .AsSplitQuery()
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync();

        var latestNotes = await _db.WeddingDetails
            .Where(d => d.PersonId.HasValue && d.Note != null)
            .GroupBy(d => d.PersonId!.Value)
            .Select(g => new { PersonId = g.Key, Content = g.OrderByDescending(d => d.Id).First().Note! })
            .ToDictionaryAsync(x => x.PersonId, x => x.Content);

        var weddingCounts = await _db.WeddingDetails
            .Where(d => d.PersonId.HasValue)
            .GroupBy(d => d.PersonId!.Value)
            .Select(g => new { PersonId = g.Key, Count = g.Select(d => d.WeddingId).Distinct().Count() })
            .ToDictionaryAsync(x => x.PersonId, x => x.Count);

        return people.Select(p => MapDto(p, latestNotes, weddingCounts)).ToList();
    }

    public async Task<List<PersonDto>> SearchAsync(string query, RoleType? roleType = null)
    {
        var q = query.Trim().ToLower();

        var maleRoles = new HashSet<RoleType> {
            RoleType.Groom, RoleType.FatherOfGroom, RoleType.FatherOfBride,
            RoleType.PaternalGrandfatherOfGroom, RoleType.PaternalGrandfatherOfBride,
            RoleType.MaternalGrandfatherOfGroom, RoleType.MaternalGrandfatherOfBride,
        };
        var femaleRoles = new HashSet<RoleType> {
            RoleType.Bride, RoleType.MotherOfGroom, RoleType.MotherOfBride,
            RoleType.PaternalGrandmotherOfGroom, RoleType.PaternalGrandmotherOfBride,
            RoleType.MaternalGrandmotherOfGroom, RoleType.MaternalGrandmotherOfBride,
        };
        var fatherGrandfatherRoles = new HashSet<RoleType> {
            RoleType.FatherOfGroom, RoleType.FatherOfBride,
            RoleType.PaternalGrandfatherOfGroom, RoleType.PaternalGrandfatherOfBride,
            RoleType.MaternalGrandfatherOfGroom, RoleType.MaternalGrandfatherOfBride,
        };
        var motherGrandmotherRoles = new HashSet<RoleType> {
            RoleType.MotherOfGroom, RoleType.MotherOfBride,
            RoleType.PaternalGrandmotherOfGroom, RoleType.PaternalGrandmotherOfBride,
            RoleType.MaternalGrandmotherOfGroom, RoleType.MaternalGrandmotherOfBride,
        };

        var baseQuery = WithGrandparents()
            .Where(p => p.FirstName.ToLower().Contains(q) || p.LastName.ToLower().Contains(q));

        if (roleType.HasValue)
        {
            if (maleRoles.Contains(roleType.Value))
                baseQuery = baseQuery.Where(p => p.Gender == Gender.Male || p.Gender == Gender.Unknown);
            else if (femaleRoles.Contains(roleType.Value))
                baseQuery = baseQuery.Where(p => p.Gender == Gender.Female || p.Gender == Gender.Unknown);

            if (fatherGrandfatherRoles.Contains(roleType.Value))
            {
                var pastGroomIds = _db.WeddingDetails
                    .Where(d => d.RoleType == RoleType.Groom && d.PersonId.HasValue)
                    .Select(d => d.PersonId!.Value);
                baseQuery = baseQuery.Where(p => !pastGroomIds.Contains(p.Id));
            }

            if (motherGrandmotherRoles.Contains(roleType.Value))
            {
                var pastBrideIds = _db.WeddingDetails
                    .Where(d => d.RoleType == RoleType.Bride && d.PersonId.HasValue)
                    .Select(d => d.PersonId!.Value);
                baseQuery = baseQuery.Where(p => !pastBrideIds.Contains(p.Id));
            }
        }

        var people = await baseQuery
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Take(20)
            .ToListAsync();

        return people.Select(p => MapDto(p)).ToList();
    }

    public async Task<PersonDto> GetByIdAsync(int id)
    {
        var person = await WithGrandparents()
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException($"Person {id} not found.");

        return MapDto(person);
    }

    public async Task<PersonProfileDto> GetProfileAsync(int id)
    {
        var person = await _db.People
            .Include(p => p.Father).ThenInclude(f => f!.Father)
            .Include(p => p.Father).ThenInclude(f => f!.Mother)
            .Include(p => p.Mother).ThenInclude(m => m!.Father)
            .Include(p => p.Mother).ThenInclude(m => m!.Mother)
            .Include(p => p.WeddingDetails).ThenInclude(d => d.Wedding).ThenInclude(w => w.Details).ThenInclude(d => d.Person)
            .Include(p => p.WeddingDetails).ThenInclude(d => d.Song)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException($"Person {id} not found.");

        var siblings = (person.FatherId.HasValue || person.MotherId.HasValue)
            ? await _db.People
                .Where(p => p.Id != id &&
                            ((person.FatherId.HasValue && p.FatherId == person.FatherId) ||
                             (person.MotherId.HasValue && p.MotherId == person.MotherId)))
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .ToListAsync()
            : new List<Person>();

        var weddings = person.WeddingDetails
            .Where(d => d.RoleType != RoleType.WeddingItself)
            .DistinctBy(d => d.WeddingId)
            .OrderByDescending(d => d.Wedding.DateOfWedding)
            .Select(d => new PersonWeddingDto
            {
                WeddingId = d.WeddingId,
                Title = WeddingTitleHelper.Compute(d.Wedding),
                Date = d.Wedding.DateOfWedding.ToString("MMM d, yyyy"),
                Location = d.Wedding.Location,
                Role = RoleHelper.GetLabel(d.RoleType),
                IsFinalized = d.Wedding.IsFinalized,
                SongTitle = d.Song?.Title,
            })
            .ToList();

        // Build notes from WeddingDetail.Note (replaces PersonNote table)
        var notes = person.WeddingDetails
            .Where(d => d.Note != null)
            .OrderByDescending(d => d.Id)
            .Select(d => new PersonNoteDto
            {
                Id = d.Id,
                PersonId = id,
                WeddingId = d.WeddingId,
                WeddingTitle = $"{WeddingTitleHelper.Compute(d.Wedding)} ({d.Wedding.DateOfWedding.Year})",
                Content = d.Note!,
                CreatedAt = DateTime.MinValue, // no separate timestamp; ordering by Id
            })
            .ToList();

        var profile = new PersonProfileDto
        {
            Id = person.Id,
            FirstName = person.FirstName,
            LastName = person.LastName,
            FullName = person.FullName,
            Gender = person.Gender.ToString().ToLower(),
            Father = person.Father != null ? ToSummary(person.Father) : null,
            Mother = person.Mother != null ? ToSummary(person.Mother) : null,
            Siblings = siblings.Select(ToSummary).ToList(),
            PaternalGrandfather = person.Father?.Father != null ? ToSummary(person.Father.Father) : null,
            PaternalGrandmother = person.Father?.Mother != null ? ToSummary(person.Father.Mother) : null,
            MaternalGrandfather = person.Mother?.Father != null ? ToSummary(person.Mother.Father) : null,
            MaternalGrandmother = person.Mother?.Mother != null ? ToSummary(person.Mother.Mother) : null,
            Weddings = weddings,
            Notes = notes,
        };

        AugmentProfileFromWeddingDetails(person, profile);

        return profile;
    }

    private static void AugmentProfileFromWeddingDetails(Person person, PersonProfileDto profile)
    {
        foreach (var d in person.WeddingDetails.DistinctBy(x => new { x.WeddingId, x.RoleType }))
        {
            var wDetails = d.Wedding.Details;

            PersonSummaryDto? Derive(RoleType rt)
            {
                var p = wDetails.FirstOrDefault(r => r.RoleType == rt && r.Person != null)?.Person;
                return p != null ? ToSummary(p) : null;
            }

            switch (d.RoleType)
            {
                case RoleType.Groom:
                    profile.Father              ??= Derive(RoleType.FatherOfGroom);
                    profile.Mother              ??= Derive(RoleType.MotherOfGroom);
                    profile.PaternalGrandfather ??= Derive(RoleType.PaternalGrandfatherOfGroom);
                    profile.PaternalGrandmother ??= Derive(RoleType.PaternalGrandmotherOfGroom);
                    profile.MaternalGrandfather ??= Derive(RoleType.MaternalGrandfatherOfGroom);
                    profile.MaternalGrandmother ??= Derive(RoleType.MaternalGrandmotherOfGroom);
                    break;
                case RoleType.Bride:
                    profile.Father              ??= Derive(RoleType.FatherOfBride);
                    profile.Mother              ??= Derive(RoleType.MotherOfBride);
                    profile.PaternalGrandfather ??= Derive(RoleType.PaternalGrandfatherOfBride);
                    profile.PaternalGrandmother ??= Derive(RoleType.PaternalGrandmotherOfBride);
                    profile.MaternalGrandfather ??= Derive(RoleType.MaternalGrandfatherOfBride);
                    profile.MaternalGrandmother ??= Derive(RoleType.MaternalGrandmotherOfBride);
                    break;
                case RoleType.FatherOfGroom:
                    profile.Father ??= Derive(RoleType.PaternalGrandfatherOfGroom);
                    profile.Mother ??= Derive(RoleType.PaternalGrandmotherOfGroom);
                    break;
                case RoleType.MotherOfGroom:
                    profile.Father ??= Derive(RoleType.MaternalGrandfatherOfGroom);
                    profile.Mother ??= Derive(RoleType.MaternalGrandmotherOfGroom);
                    break;
                case RoleType.FatherOfBride:
                    profile.Father ??= Derive(RoleType.PaternalGrandfatherOfBride);
                    profile.Mother ??= Derive(RoleType.PaternalGrandmotherOfBride);
                    break;
                case RoleType.MotherOfBride:
                    profile.Father ??= Derive(RoleType.MaternalGrandfatherOfBride);
                    profile.Mother ??= Derive(RoleType.MaternalGrandmotherOfBride);
                    break;
            }
        }
    }

    private static PersonSummaryDto ToSummary(Person p) => new()
    {
        Id = p.Id,
        FullName = p.FullName,
        Gender = p.Gender.ToString().ToLower()
    };

    public async Task<PersonDto> CreateAsync(CreatePersonDto dto)
    {
        var person = new Person
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Gender = dto.Gender,
            FatherId = dto.FatherId,
            MotherId = dto.MotherId,
            FamilyGroup = dto.FamilyGroup?.Trim(),
        };
        _db.People.Add(person);
        await _db.SaveChangesAsync();

        await SyncParentRelationshipsAsync(person.Id, person.Gender, dto.FatherId, dto.MotherId);

        return await GetByIdAsync(person.Id);
    }

    public async Task<PersonDto> UpdateAsync(int id, UpdatePersonDto dto)
    {
        var person = await _db.People.FindAsync(id)
            ?? throw new KeyNotFoundException($"Person {id} not found.");

        person.FirstName = dto.FirstName.Trim();
        person.LastName = dto.LastName.Trim();
        person.Gender = dto.Gender;
        person.FatherId = dto.FatherId;
        person.MotherId = dto.MotherId;
        person.FamilyGroup = dto.FamilyGroup?.Trim();

        await _db.SaveChangesAsync();

        await SyncParentRelationshipsAsync(id, dto.Gender, dto.FatherId, dto.MotherId);

        return await GetByIdAsync(id);
    }

    private async Task SyncParentRelationshipsAsync(int personId, Gender gender, int? fatherId, int? motherId)
    {
        const int FatherTypeId   = 1;
        const int MotherTypeId   = 2;
        const int SonTypeId      = 3;
        const int DaughterTypeId = 4;

        int childTypeId = gender == Gender.Female ? DaughterTypeId : SonTypeId;

        var oldRels = await _db.PersonRelationships
            .Where(r =>
                (r.ToPersonId   == personId && (r.RelationshipTypeId == FatherTypeId || r.RelationshipTypeId == MotherTypeId)) ||
                (r.FromPersonId == personId && (r.RelationshipTypeId == SonTypeId    || r.RelationshipTypeId == DaughterTypeId)))
            .ToListAsync();
        _db.PersonRelationships.RemoveRange(oldRels);

        var newRels = new List<PersonRelationship>();

        if (fatherId.HasValue)
        {
            newRels.Add(new PersonRelationship { FromPersonId = fatherId.Value, ToPersonId = personId,       RelationshipTypeId = FatherTypeId, IsActive = true, CreatedAt = DateTime.UtcNow });
            newRels.Add(new PersonRelationship { FromPersonId = personId,       ToPersonId = fatherId.Value, RelationshipTypeId = childTypeId,  IsActive = true, CreatedAt = DateTime.UtcNow });
        }

        if (motherId.HasValue)
        {
            newRels.Add(new PersonRelationship { FromPersonId = motherId.Value, ToPersonId = personId,       RelationshipTypeId = MotherTypeId, IsActive = true, CreatedAt = DateTime.UtcNow });
            newRels.Add(new PersonRelationship { FromPersonId = personId,       ToPersonId = motherId.Value, RelationshipTypeId = childTypeId,  IsActive = true, CreatedAt = DateTime.UtcNow });
        }

        if (newRels.Count > 0)
        {
            _db.PersonRelationships.AddRange(newRels);
            await _db.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        var person = await _db.People.FindAsync(id)
            ?? throw new KeyNotFoundException($"Person {id} not found.");

        var isInWedding = await _db.WeddingDetails.AnyAsync(d => d.PersonId == id);
        if (isInWedding)
            throw new DomainException("Cannot delete a person who is assigned to a wedding role.");

        // Clear FatherId/MotherId on any children that reference this person
        var children = await _db.People
            .Where(p => p.FatherId == id || p.MotherId == id)
            .ToListAsync();
        foreach (var child in children)
        {
            if (child.FatherId == id) child.FatherId = null;
            if (child.MotherId == id) child.MotherId = null;
        }

        // Delete all PersonRelationship rows involving this person
        var rels = await _db.PersonRelationships
            .Where(r => r.FromPersonId == id || r.ToPersonId == id)
            .ToListAsync();
        _db.PersonRelationships.RemoveRange(rels);

        _db.People.Remove(person);
        await _db.SaveChangesAsync();
    }

    private IQueryable<Person> WithGrandparents() => _db.People
        .Include(p => p.Father).ThenInclude(f => f!.Father)
        .Include(p => p.Father).ThenInclude(f => f!.Mother)
        .Include(p => p.Mother).ThenInclude(m => m!.Father)
        .Include(p => p.Mother).ThenInclude(m => m!.Mother);

    private static PersonDto MapDto(Person p, Dictionary<int, string>? latestNotes = null, Dictionary<int, int>? weddingCounts = null) => new()
    {
        Id = p.Id,
        FirstName = p.FirstName,
        LastName = p.LastName,
        FullName = p.FullName,
        Gender = p.Gender.ToString().ToLower(),
        FamilyGroup = p.FamilyGroup,
        WeddingCount = weddingCounts?.GetValueOrDefault(p.Id) ?? 0,
        LastNote = latestNotes?.GetValueOrDefault(p.Id),
        FatherId = p.FatherId,
        FatherName = p.Father?.FullName,
        MotherId = p.MotherId,
        MotherName = p.Mother?.FullName,
        PaternalGrandfatherId = p.Father?.FatherId,
        PaternalGrandfatherName = p.Father?.Father?.FullName,
        PaternalGrandmotherId = p.Father?.MotherId,
        PaternalGrandmotherName = p.Father?.Mother?.FullName,
        MaternalGrandfatherId = p.Mother?.FatherId,
        MaternalGrandfatherName = p.Mother?.Father?.FullName,
        MaternalGrandmotherId = p.Mother?.MotherId,
        MaternalGrandmotherName = p.Mother?.Mother?.FullName
    };
}
