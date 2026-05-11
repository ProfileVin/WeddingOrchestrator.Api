using Microsoft.AspNetCore.Mvc;
using WeddingOrchestrator.Api.DTOs.People;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Controllers;

[ApiController]
[Route("api/people")]
public class PeopleController : ControllerBase
{
    private readonly IPersonService _people;

    public PeopleController(IPersonService people) => _people = people;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _people.GetAllAsync());

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q = "") =>
        Ok(await _people.SearchAsync(q));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(await _people.GetByIdAsync(id));

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
}
