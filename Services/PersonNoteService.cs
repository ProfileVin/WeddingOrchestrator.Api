using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.People;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class PersonNoteService : IPersonNoteService
{
    private readonly AppDbContext _db;

    public PersonNoteService(AppDbContext db) => _db = db;

    public async Task<List<PersonNoteDto>> GetByPersonAsync(int personId)
    {
        var notes = await _db.PersonNotes
            .Where(n => n.PersonId == personId)
            .Include(n => n.Wedding).ThenInclude(w => w!.Roles).ThenInclude(r => r.Person)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return notes.Select(MapDto).ToList();
    }

    public async Task<PersonNoteDto> CreateAsync(int personId, CreatePersonNoteDto dto)
    {
        var exists = await _db.People.AnyAsync(p => p.Id == personId);
        if (!exists) throw new KeyNotFoundException($"Person {personId} not found.");

        var note = new PersonNote
        {
            PersonId = personId,
            WeddingId = dto.WeddingId,
            Content = dto.Content.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        _db.PersonNotes.Add(note);
        await _db.SaveChangesAsync();

        await _db.Entry(note).Reference(n => n.Wedding).Query()
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .LoadAsync();
        return MapDto(note);
    }

    public async Task<PersonNoteDto> UpdateAsync(int noteId, UpdatePersonNoteDto dto)
    {
        var note = await _db.PersonNotes
            .Include(n => n.Wedding)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException($"Note {noteId} not found.");

        note.Content = dto.Content.Trim();
        await _db.SaveChangesAsync();
        return MapDto(note);
    }

    public async Task DeleteAsync(int noteId)
    {
        var note = await _db.PersonNotes.FindAsync(noteId)
            ?? throw new KeyNotFoundException($"Note {noteId} not found.");

        _db.PersonNotes.Remove(note);
        await _db.SaveChangesAsync();
    }

    private static PersonNoteDto MapDto(PersonNote n) => new()
    {
        Id = n.Id,
        PersonId = n.PersonId,
        WeddingId = n.WeddingId,
        WeddingTitle = n.Wedding != null
            ? $"{WeddingTitleHelper.Compute(n.Wedding)} ({n.Wedding.DateOfWedding.Year})"
            : null,
        Content = n.Content,
        CreatedAt = n.CreatedAt,
    };
}
