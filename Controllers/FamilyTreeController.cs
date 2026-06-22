using Microsoft.AspNetCore.Mvc;
using WeddingOrchestrator.Api.DTOs.FamilyTree;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Controllers;

[ApiController]
[Route("api/family-tree")]
public class FamilyTreeController : ControllerBase
{
    private readonly IFamilyTreeService _service;
    public FamilyTreeController(IFamilyTreeService service) => _service = service;

    [HttpGet("relationship-types")]
    public async Task<ActionResult<List<RelationshipTypeDto>>> GetRelationshipTypes()
        => Ok(await _service.GetRelationshipTypesAsync());

    [HttpGet("family/{lastName}")]
    public async Task<ActionResult<FamilyTreeDataDto>> GetFamilyTree(string lastName)
        => Ok(await _service.GetFamilyTreeByLastNameAsync(lastName));

    [HttpGet("people/{personId:int}/relationships")]
    public async Task<ActionResult<List<PersonRelationshipDto>>> GetPersonRelationships(int personId)
        => Ok(await _service.GetPersonRelationshipsAsync(personId));

    [HttpPost("relationships")]
    public async Task<ActionResult<PersonRelationshipDto>> CreateRelationship([FromBody] CreateRelationshipDto dto)
        => Ok(await _service.CreateRelationshipAsync(dto));

    [HttpDelete("relationships/{id:int}")]
    public async Task<IActionResult> DeleteRelationship(int id)
    {
        await _service.DeleteRelationshipAsync(id);
        return NoContent();
    }
}
