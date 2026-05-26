using Microsoft.AspNetCore.Mvc;
using WeddingOrchestrator.Api.DTOs.Songs;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Controllers;

[ApiController]
[Route("api/songs")]
public class SongsController : ControllerBase
{
    private readonly ISongService _songs;

    public SongsController(ISongService songs) => _songs = songs;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _songs.GetAllSongsAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(await _songs.GetByIdAsync(id));

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] int categoryId, [FromForm] string title)
    {
        var dto = await _songs.UploadSongAsync(file, categoryId, title);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateSongDto dto)
    {
        var result = await _songs.CreateBlankSongAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSongDto dto) =>
        Ok(await _songs.UpdateSongAsync(id, dto));

    [HttpPut("{id:int}/file")]
    public async Task<IActionResult> ReplaceFile(int id, [FromForm] IFormFile file) =>
        Ok(await _songs.ReplaceFileAsync(id, file));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _songs.DeleteSongAsync(id);
        return NoContent();
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var (stream, fileName, contentType) = await _songs.DownloadSongAsync(id);
        return File(stream, contentType, fileName);
    }

    [HttpGet("{id:int}/open")]
    public async Task<IActionResult> OpenInWord(int id)
    {
        await _songs.OpenInWordAsync(id);
        return Ok(new { message = "Opening in Word..." });
    }
}
