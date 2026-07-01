using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.Weddings;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class ConflictDetectionService : IConflictDetectionService
{
    private readonly AppDbContext _db;

    public ConflictDetectionService(AppDbContext db) => _db = db;

    public async Task<ConflictReportDto> GetConflictReportAsync(int weddingId)
    {
        var newPersonIds = await _db.WeddingDetails
            .Where(d => d.WeddingId == weddingId && d.PersonId != null)
            .Select(d => d.PersonId!.Value)
            .Distinct()
            .ToListAsync();

        if (!newPersonIds.Any())
            return new ConflictReportDto();

        var conflictingWeddingIds = await _db.WeddingDetails
            .Where(d => d.PersonId != null && newPersonIds.Contains(d.PersonId.Value) && d.WeddingId != weddingId)
            .Select(d => d.WeddingId)
            .Distinct()
            .ToListAsync();

        if (!conflictingWeddingIds.Any())
            return new ConflictReportDto();

        var conflictingWeddings = await _db.Weddings
            .Where(w => conflictingWeddingIds.Contains(w.Id))
            .Include(w => w.Details).ThenInclude(d => d.Person)
            .Include(w => w.Details).ThenInclude(d => d.Song!).ThenInclude(s => s.Category)
            .AsSplitQuery()
            .ToListAsync();

        var report = new ConflictReportDto();
        var allForbiddenIds = new HashSet<int>();

        foreach (var cw in conflictingWeddings)
        {
            var cwDto = new ConflictingWeddingDto
            {
                WeddingId = cw.Id,
                WeddingTitle = WeddingTitleHelper.Compute(cw)
            };

            foreach (var detail in cw.Details.Where(d => d.PersonId != null && newPersonIds.Contains(d.PersonId.Value)))
            {
                cwDto.SharedPeople.Add(new SharedPersonDto
                {
                    PersonId = detail.PersonId!.Value,
                    PersonName = detail.Person?.FullName ?? string.Empty,
                    RoleInThatWedding = RoleHelper.GetLabel(detail.RoleType),
                    SongsHeard = detail.SongId.HasValue && detail.Song != null
                        ? new List<string> { detail.Song.Title }
                        : new List<string>()
                });
            }

            var songsByCat = cw.Details
                .Where(d => d.SongId.HasValue && d.Song != null)
                .GroupBy(d => d.Song!.Category?.Name ?? string.Empty)
                .OrderBy(g => g.Key);

            foreach (var catGroup in songsByCat)
            {
                var catDto = new ForbiddenCategoryDto { CategoryName = catGroup.Key };
                foreach (var detail in catGroup)
                {
                    catDto.Songs.Add(new ForbiddenSongDto
                    {
                        SongId = detail.SongId!.Value,
                        SongTitle = detail.Song!.Title
                    });
                    allForbiddenIds.Add(detail.SongId!.Value);
                }
                cwDto.ForbiddenSongsByCategory.Add(catDto);
            }

            report.ConflictingWeddings.Add(cwDto);
        }

        report.ForbiddenSongIds = allForbiddenIds.ToList();
        return report;
    }

    public async Task<HashSet<int>> GetForbiddenSongIdsAsync(int weddingId)
    {
        var report = await GetConflictReportAsync(weddingId);
        return report.ForbiddenSongIds.ToHashSet();
    }
}
