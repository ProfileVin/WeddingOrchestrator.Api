using Microsoft.AspNetCore.Mvc;
using WeddingOrchestrator.Api.DTOs.Songs;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Controllers;

[ApiController]
[Route("api/song-categories")]
public class SongCategoriesController : ControllerBase
{
    private readonly ISongService _songs;

    public SongCategoriesController(ISongService songs) => _songs = songs;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _songs.GetAllCategoriesWithSongsAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSongCategoryDto dto) =>
        Ok(await _songs.CreateCategoryAsync(dto));
}
