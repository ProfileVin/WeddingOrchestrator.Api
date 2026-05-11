using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.People;
using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class PersonService : IPersonService
{
    private readonly AppDbContext _db;

    public PersonService(AppDbContext db) => _db = db;

    public async Task<List<PersonDto>> GetAllAsync()
    {
        var people = await _db.People
            .Include(p => p.Father).ThenInclude(f => f!.Father)
            .Include(p => p.Father).ThenInclude(f => f!.Mother)
            .Include(p => p.Mother).ThenInclude(m => m!.Father)
            .Include(p => p.Mother).ThenInclude(m => m!.Mother)
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync();

        return people.Select(MapDto).ToList();
    }

    public async Task<List<PersonDto>> SearchAsync(string query)
    {
        var q = query.Trim().ToLower();
        var people = await _db.People
            .Include(p => p.Father).ThenInclude(f => f!.Father)
            .Include(p => p.Father).ThenInclude(f => f!.Mother)
            .Include(p => p.Mother).ThenInclude(m => m!.Father)
            .Include(p => p.Mother).ThenInclude(m => m!.Mother)
            .Where(p => (p.FirstName + " " + p.LastName).ToLower().Contains(q))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Take(20)
            .ToListAsync();

        return people.Select(MapDto).ToList();
    }

    public async Task<PersonDto> GetByIdAsync(int id)
    {
        var person = await _db.People
            .Include(p => p.Father).ThenInclude(f => f!.Father)
            .Include(p => p.Father).ThenInclude(f => f!.Mother)
            .Include(p => p.Mother).ThenInclude(m => m!.Father)
            .Include(p => p.Mother).ThenInclude(m => m!.Mother)
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
        person.FatherId = dto.FatherId;
        person.MotherId = dto.MotherId;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var person = await _db.People.FindAsync(id)
            ?? throw new KeyNotFoundException($"Person {id} not found.");

        var isReferencedAsParent = await _db.People
            .AnyAsync(p => p.FatherId == id || p.MotherId == id);

        if (isReferencedAsParent)
            throw new InvalidOperationException("Cannot delete a person who is linked as a parent. Remove the parent link first.");

        _db.People.Remove(person);
        await _db.SaveChangesAsync();
    }

    private static PersonDto MapDto(Person p) => new()
    {
        Id = p.Id,
        FirstName = p.FirstName,
        LastName = p.LastName,
        FullName = p.FullName,
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
