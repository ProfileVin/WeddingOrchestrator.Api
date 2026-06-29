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
            .Include(p => p.WeddingDetails)
            .Where(p => p.FirstName.ToLower().Contains(lowerTerm)
                     || p.LastName.ToLower().Contains(lowerTerm)
                     || (p.FirstName.ToLower() + " " + p.LastName.ToLower()).Contains(lowerTerm))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Take(10)
            .ToListAsync();

        var people = rawPeople.Select(p => new SearchPersonResult
        {
            Id = p.Id,
            FullName = p.FullName,
            Gender = p.Gender.ToString().ToLower(),
            Roles = p.WeddingDetails
                .Where(d => d.RoleType != RoleType.WeddingItself)
                .Select(d => RoleHelper.GetLabel(d.RoleType))
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
            CategoryId = s.SongCategoryId,
            CategoryName = s.Category?.Name ?? string.Empty
        }).ToList();

        // Collect IDs via base match (location or any person name)
        var weddingIds = await _db.Weddings
            .Where(w => (w.Location != null && w.Location.ToLower().Contains(lowerTerm))
                || w.Details.Any(d => d.Person != null &&
                    (d.Person.FirstName.ToLower().Contains(lowerTerm) ||
                     d.Person.LastName.ToLower().Contains(lowerTerm) ||
                     (d.Person.FirstName.ToLower() + " " + d.Person.LastName.ToLower()).Contains(lowerTerm))))
            .Select(w => w.Id)
            .ToListAsync();

        // Also match the computed title format "GroomLastName - BrideLastName" (partial typing supported)
        // Detect " -" so results appear even before the user finishes typing " - BrideName"
        var dashIndex = lowerTerm.IndexOf(" -", StringComparison.Ordinal);
        if (dashIndex >= 0)
        {
            var groomPart = lowerTerm[..dashIndex].Trim();
            var bridePart = lowerTerm[(dashIndex + 2)..].TrimStart().Trim();

            if (!string.IsNullOrEmpty(groomPart))
            {
                var gp = groomPart;
                var bp = bridePart;
                var titleMatchIds = await _db.Weddings
                    .Where(w =>
                        w.Details.Any(d => d.RoleType == RoleType.Groom && d.Person != null
                            && d.Person.LastName.ToLower().Contains(gp))
                        && (bp == string.Empty || w.Details.Any(d => d.RoleType == RoleType.Bride && d.Person != null
                            && d.Person.LastName.ToLower().Contains(bp))))
                    .Select(w => w.Id)
                    .ToListAsync();
                weddingIds = weddingIds.Union(titleMatchIds).ToList();
            }
        }

        var rawWeddings = await _db.Weddings
            .Include(w => w.Details).ThenInclude(d => d.Person)
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
