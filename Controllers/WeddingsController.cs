using Microsoft.AspNetCore.Mvc;
using WeddingOrchestrator.Api.DTOs.Weddings;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Controllers;

[ApiController]
[Route("api/weddings")]
public class WeddingsController : ControllerBase
{
    private readonly IWeddingService _weddings;
    private readonly IConflictDetectionService _conflicts;
    private readonly IWeddingFolderService _folder;

    public WeddingsController(IWeddingService weddings, IConflictDetectionService conflicts, IWeddingFolderService folder)
    {
        _weddings = weddings;
        _conflicts = conflicts;
        _folder = folder;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _weddings.GetAllAsync());

    [HttpGet("check-availability")]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] DateTime date,
        [FromQuery] TimeOnly? startTime,
        [FromQuery] TimeOnly? endTime) =>
        Ok(await _weddings.CheckAvailabilityAsync(date, startTime, endTime));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(await _weddings.GetByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWeddingDto dto)
    {
        var result = await _weddings.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}/roles")]
    public async Task<IActionResult> UpdateRoles(int id, [FromBody] WeddingFamilyTreeDto dto) =>
        Ok(await _weddings.UpdateRolesAsync(id, dto));

    [HttpPut("{id:int}/songs")]
    public async Task<IActionResult> AssignSongs(int id, [FromBody] AssignSongsDto dto) =>
        Ok(await _weddings.AssignSongsAsync(id, dto));

    [HttpGet("{id:int}/conflicts")]
    public async Task<IActionResult> GetConflicts(int id) =>
        Ok(await _conflicts.GetConflictReportAsync(id));

    [HttpPost("{id:int}/finalize")]
    public async Task<IActionResult> Finalize(int id) =>
        Ok(await _weddings.FinalizeAsync(id));

    [HttpPost("{id:int}/unfinalize")]
    public async Task<IActionResult> Unfinalize(int id) =>
        Ok(await _weddings.UnfinalizeAsync(id));

    [HttpPost("{id:int}/other-relations")]
    public async Task<IActionResult> LinkOtherRelations(int id, [FromBody] List<LinkOtherRelationDto> dto) =>
        Ok(await _weddings.LinkOtherRelationsAsync(id, dto));

    [HttpDelete("{id:int}/other-relations/{personId:int}")]
    public async Task<IActionResult> DeleteOtherRelation(int id, int personId)
    {
        await _weddings.DeleteOtherRelationAsync(id, personId);
        return NoContent();
    }

    [HttpPut("{id:int}/detail-note")]
    public async Task<IActionResult> UpdateDetailNote(int id, [FromBody] UpdateDetailNoteDto dto) =>
        Ok(await _weddings.UpdateDetailNoteAsync(id, dto));

    [HttpPut("{id:int}/song-intros")]
    public async Task<IActionResult> UpdateSongIntros(int id, [FromBody] UpdateWeddingSongIntrosDto dto) =>
        Ok(await _weddings.UpdateWeddingSongIntrosAsync(id, dto));

    [HttpPost("{id:int}/sync-folder")]
    public async Task<IActionResult> SyncFolder(int id)
    {
        await _folder.SyncFolderAsync(id);
        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _weddings.DeleteAsync(id);
        return NoContent();
    }
}
