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

    public WeddingsController(IWeddingService weddings, IConflictDetectionService conflicts)
    {
        _weddings = weddings;
        _conflicts = conflicts;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _weddings.GetAllAsync());

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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _weddings.DeleteAsync(id);
        return NoContent();
    }
}
