using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.Search;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _db;

    public SearchController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q = "")
    {
        var term = q.Trim();
        if (term.Length == 0)
            return Ok(new GlobalSearchResultDto());

        var lowerTerm = term.ToLower();

        var rawPeople = await _db.People
            .Include(p => p.WeddingRoles)
            .Where(p => p.FirstName.ToLower().Contains(lowerTerm) || p.LastName.ToLower().Contains(lowerTerm))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Take(10)
            .ToListAsync();

        var people = rawPeople.Select(p => new SearchPersonResult
        {
            Id = p.Id,
            FullName = p.FullName,
            Gender = p.Gender.ToString().ToLower(),
            Roles = p.WeddingRoles
                .Where(wr => wr.RoleType != RoleType.WeddingItself)
                .Select(wr => RoleHelper.GetLabel(wr.RoleType))
                .Distinct()
                .Take(2)
                .ToList()
        }).ToList();

        var rawSongs = await _db.Songs
            .Include(s => s.Category)
            .Where(s => s.Title.ToLower().Contains(lowerTerm))
            .OrderBy(s => s.Title)
            .Take(5)
            .ToListAsync();

        var songs = rawSongs.Select(s => new SearchSongResult
        {
            Id = s.Id,
            Title = s.Title,
            CategoryName = s.Category?.Name ?? string.Empty
        }).ToList();

        return Ok(new GlobalSearchResultDto { People = people, Songs = songs });
    }
}
