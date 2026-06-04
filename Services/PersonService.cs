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
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync();

        return people.Select(MapDto).ToList();
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
                var pastGroomIds = _db.WeddingRoles
                    .Where(r => r.RoleType == RoleType.Groom && r.PersonId.HasValue)
                    .Select(r => r.PersonId!.Value);
                baseQuery = baseQuery.Where(p => !pastGroomIds.Contains(p.Id));
            }

            if (motherGrandmotherRoles.Contains(roleType.Value))
            {
                var pastBrideIds = _db.WeddingRoles
                    .Where(r => r.RoleType == RoleType.Bride && r.PersonId.HasValue)
                    .Select(r => r.PersonId!.Value);
                baseQuery = baseQuery.Where(p => !pastBrideIds.Contains(p.Id));
            }
        }

        var people = await baseQuery
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Take(20)
            .ToListAsync();

        return people.Select(MapDto).ToList();
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
            .Include(p => p.WeddingRoles).ThenInclude(wr => wr.Wedding).ThenInclude(w => w.Roles).ThenInclude(r => r.Person)
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

        var weddings = person.WeddingRoles
            .Where(wr => wr.RoleType != RoleType.WeddingItself)
            .OrderByDescending(wr => wr.Wedding.DateOfWedding)
            .Select(wr => new PersonWeddingDto
            {
                WeddingId = wr.WeddingId,
                Title = WeddingTitleHelper.Compute(wr.Wedding),
                Date = wr.Wedding.DateOfWedding.ToString("MMM d, yyyy"),
                Role = RoleHelper.GetLabel(wr.RoleType),
                IsFinalized = wr.Wedding.IsFinalized
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
            Weddings = weddings
        };

        AugmentProfileFromWeddingRoles(person, profile);

        return profile;
    }

    private static void AugmentProfileFromWeddingRoles(Person person, PersonProfileDto profile)
    {
        foreach (var wr in person.WeddingRoles)
        {
            var wRoles = wr.Wedding.Roles;

            PersonSummaryDto? Derive(RoleType rt)
            {
                var p = wRoles.FirstOrDefault(r => r.RoleType == rt && r.Person != null)?.Person;
                return p != null ? ToSummary(p) : null;
            }

            switch (wr.RoleType)
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
            MotherId = dto.MotherId
        };
        _db.People.Add(person);
        await _db.SaveChangesAsync();
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

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var person = await _db.People.FindAsync(id)
            ?? throw new KeyNotFoundException($"Person {id} not found.");

        var isParent = await _db.People.AnyAsync(p => p.FatherId == id || p.MotherId == id);
        if (isParent)
            throw new DomainException("Cannot delete a person who is linked as a parent. Remove the parent link first.");

        var isInWedding = await _db.WeddingRoles.AnyAsync(r => r.PersonId == id);
        if (isInWedding)
            throw new DomainException("Cannot delete a person who is assigned to a wedding role.");

        _db.People.Remove(person);
        await _db.SaveChangesAsync();
    }

    private IQueryable<Person> WithGrandparents() => _db.People
        .Include(p => p.Father).ThenInclude(f => f!.Father)
        .Include(p => p.Father).ThenInclude(f => f!.Mother)
        .Include(p => p.Mother).ThenInclude(m => m!.Father)
        .Include(p => p.Mother).ThenInclude(m => m!.Mother);

    private static PersonDto MapDto(Person p) => new()
    {
        Id = p.Id,
        FirstName = p.FirstName,
        LastName = p.LastName,
        FullName = p.FullName,
        Gender = p.Gender.ToString().ToLower(),
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
