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
    private static readonly string[] TitleSeparator = [" - "];

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

        // Collect IDs via base match (location or any person name)
        var weddingIds = await _db.Weddings
            .Where(w => (w.Location != null && w.Location.ToLower().Contains(lowerTerm))
                || w.Roles.Any(r => r.Person != null &&
                    (r.Person.FirstName.ToLower().Contains(lowerTerm) ||
                     r.Person.LastName.ToLower().Contains(lowerTerm))))
            .Select(w => w.Id)
            .ToListAsync();

        // Also match the computed title format "GroomLastName - BrideLastName"
        var titleParts = lowerTerm.Split(TitleSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (titleParts.Length == 2)
        {
            var p0 = titleParts[0].Trim();
            var p1 = titleParts[1].Trim();
            var titleMatchIds = await _db.Weddings
                .Where(w =>
                    w.Roles.Any(r => r.RoleType == RoleType.Groom && r.Person != null
                        && r.Person.LastName.ToLower().Contains(p0))
                    && w.Roles.Any(r => r.RoleType == RoleType.Bride && r.Person != null
                        && r.Person.LastName.ToLower().Contains(p1)))
                .Select(w => w.Id)
                .ToListAsync();
            weddingIds = weddingIds.Union(titleMatchIds).ToList();
        }

        var rawWeddings = await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .Where(w => weddingIds.Contains(w.Id))
            .OrderByDescending(w => w.DateOfWedding)
            .Take(5)
            .ToListAsync();

        var weddings = rawWeddings.Select(w => new SearchWeddingResult
        {
            Id = w.Id,
            Title = WeddingTitleHelper.Compute(w),
            Date = w.DateOfWedding.ToString("MMM d, yyyy"),
            Location = w.Location,
            IsFinalized = w.IsFinalized,
        }).ToList();

        return Ok(new GlobalSearchResultDto { People = people, Songs = songs, Weddings = weddings });
    }
}
