using Microsoft.AspNetCore.Mvc;
using WeddingOrchestrator.Api.DTOs.People;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Controllers;

[ApiController]
[Route("api/people")]
public class PeopleController : ControllerBase
{
    private readonly IPersonService _people;
    private readonly IPersonNoteService _notes;

    public PeopleController(IPersonService people, IPersonNoteService notes)
    {
        _people = people;
        _notes = notes;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _people.GetAllAsync());

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q = "", [FromQuery] RoleType? roleType = null) =>
        Ok(await _people.SearchAsync(q, roleType));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(await _people.GetByIdAsync(id));

    [HttpGet("{id:int}/profile")]
    public async Task<IActionResult> GetProfile(int id) =>
        Ok(await _people.GetProfileAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePersonDto dto)
    {
        var result = await _people.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePersonDto dto) =>
        Ok(await _people.UpdateAsync(id, dto));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _people.DeleteAsync(id);
        return NoContent();
    }

    // ── Notes ─────────────────────────────────────────────────────────────────

    [HttpGet("{personId:int}/notes")]
    public async Task<IActionResult> GetNotes(int personId) =>
        Ok(await _notes.GetByPersonAsync(personId));

    [HttpPost("{personId:int}/notes")]
    public async Task<IActionResult> CreateNote(int personId, [FromBody] CreatePersonNoteDto dto) =>
        Ok(await _notes.CreateAsync(personId, dto));

    [HttpPut("{personId:int}/notes/{noteId:int}")]
    public async Task<IActionResult> UpdateNote(int personId, int noteId, [FromBody] UpdatePersonNoteDto dto) =>
        Ok(await _notes.UpdateAsync(noteId, dto));

    [HttpDelete("{personId:int}/notes/{noteId:int}")]
    public async Task<IActionResult> DeleteNote(int personId, int noteId)
    {
        await _notes.DeleteAsync(noteId);
        return NoContent();
    }
}
