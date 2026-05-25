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
        // Step 1: all people in the new wedding
        var newPersonIds = await _db.WeddingRoles
            .Where(r => r.WeddingId == weddingId && r.PersonId != null)
            .Select(r => r.PersonId!.Value)
            .Distinct()
            .ToListAsync();

        if (!newPersonIds.Any())
            return new ConflictReportDto();

        // Step 2: other weddings that share at least one person
        var conflictingWeddingIds = await _db.WeddingRoles
            .Where(r => r.PersonId != null && newPersonIds.Contains(r.PersonId.Value) && r.WeddingId != weddingId)
            .Select(r => r.WeddingId)
            .Distinct()
            .ToListAsync();

        if (!conflictingWeddingIds.Any())
            return new ConflictReportDto();

        // Step 3: load conflicting weddings with full detail
        var conflictingWeddings = await _db.Weddings
            .Where(w => conflictingWeddingIds.Contains(w.Id))
            .Include(w => w.Roles)
                .ThenInclude(r => r.Person)
            .Include(w => w.Roles)
                .ThenInclude(r => r.SongAssignments)
                    .ThenInclude(a => a.Song)
                        .ThenInclude(s => s.Category)
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

            // Which people are shared?
            foreach (var role in cw.Roles.Where(r => r.PersonId != null && newPersonIds.Contains(r.PersonId.Value)))
            {
                cwDto.SharedPeople.Add(new SharedPersonDto
                {
                    PersonId = role.PersonId!.Value,
                    PersonName = role.Person?.FullName ?? string.Empty,
                    RoleInThatWedding = RoleHelper.GetLabel(role.RoleType),
                    SongsHeard = role.SongAssignments.Select(a => a.Song.Title).ToList()
                });
            }

            // All songs used in that wedding, grouped by category
            var songsByCat = cw.Roles
                .SelectMany(r => r.SongAssignments)
                .GroupBy(a => a.Song.Category.Name)
                .OrderBy(g => g.Key);

            foreach (var catGroup in songsByCat)
            {
                var catDto = new ForbiddenCategoryDto { CategoryName = catGroup.Key };
                foreach (var assignment in catGroup)
                {
                    catDto.Songs.Add(new ForbiddenSongDto
                    {
                        SongId = assignment.SongId,
                        SongTitle = assignment.Song.Title
                    });
                    allForbiddenIds.Add(assignment.SongId);
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
