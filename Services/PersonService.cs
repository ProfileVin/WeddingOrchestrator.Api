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
