using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class WeddingFolderService : IWeddingFolderService
{
    private readonly AppDbContext _db;
    private readonly string _songStorageRoot;
    private readonly string _weddingOutputRoot;

    public WeddingFolderService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _songStorageRoot = config["SongStoragePath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "wedding-orchestrator", "songs");
        _weddingOutputRoot = config["WeddingOutputPath"]
            ?? @"C:\Applications\wedding-orchestrator\weddings";
    }

    public async Task SyncFolderAsync(int weddingId)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Details).ThenInclude(d => d.Person)
            .Include(w => w.Details).ThenInclude(d => d.Song)
            .Include(w => w.WeddingSongIntro)
            .Include(w => w.FatherMotherWeddingSongIntroGroom)
            .Include(w => w.FatherMotherWeddingSongIntroBride)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var weddingDir  = ResolveWeddingDir(wedding, weddingId);
        MarkWeddingDirOwnership(weddingDir, weddingId);
        var songsFolder = Path.Combine(weddingDir, "Songs");
        Directory.CreateDirectory(songsFolder);

        var assignments = new List<(string RoleLabel, string Title, string RelativeFilePath)>();

        assignments.AddRange(wedding.Details
            .Where(d => d.SongId.HasValue && d.Song != null)
            .Select(d => (RoleHelper.GetLabel(d.RoleType), d.Song!.Title, d.Song.RelativeFilePath)));

        if (wedding.WeddingSongIntro != null)
            assignments.Add(("Wedding Intro", wedding.WeddingSongIntro.Title, wedding.WeddingSongIntro.RelativeFilePath));
        if (wedding.FatherMotherWeddingSongIntroGroom != null)
            assignments.Add(("Father Mother Groom Intro", wedding.FatherMotherWeddingSongIntroGroom.Title, wedding.FatherMotherWeddingSongIntroGroom.RelativeFilePath));
        if (wedding.FatherMotherWeddingSongIntroBride != null)
            assignments.Add(("Father Mother Bride Intro", wedding.FatherMotherWeddingSongIntroBride.Title, wedding.FatherMotherWeddingSongIntroBride.RelativeFilePath));

        var expectedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (roleLabel, title, relativePath) in assignments)
        {
            var sourcePath = Path.Combine(_songStorageRoot, relativePath);
            if (!File.Exists(sourcePath)) continue;

            var destFileName = SanitizeFileName($"{roleLabel}_{title}") + ".docx";
            var destPath = Path.Combine(songsFolder, destFileName);

            expectedFileNames.Add(destFileName);
            File.Copy(sourcePath, destPath, overwrite: true);
        }

        // Remove files in the Songs folder that are no longer assigned
        foreach (var existing in Directory.GetFiles(songsFolder, "*.docx"))
        {
            if (!expectedFileNames.Contains(Path.GetFileName(existing)))
                File.Delete(existing);
        }

        // Auto-generate the combined songs CSV
        var personIds = wedding.Details
            .Where(d => d.PersonId.HasValue)
            .Select(d => d.PersonId!.Value)
            .Distinct()
            .ToList();
        var pastDetails = await QueryPastDetailsAsync(personIds, weddingId);
        await GenerateCombinedSongsCsvAsync(wedding, pastDetails, weddingDir);
    }

    public async Task OpenCombinedSongsTxtAsync(int weddingId)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Details).ThenInclude(d => d.Person)
            .Include(w => w.Details).ThenInclude(d => d.Song)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var weddingDir = ResolveWeddingDir(wedding, weddingId);
        MarkWeddingDirOwnership(weddingDir, weddingId);

        var personIds = wedding.Details
            .Where(d => d.PersonId.HasValue)
            .Select(d => d.PersonId!.Value)
            .Distinct()
            .ToList();
        var pastDetails = await QueryPastDetailsAsync(personIds, weddingId);

        // Always regenerate so the file is never stale or missing
        await GenerateCombinedSongsCsvAsync(wedding, pastDetails, weddingDir);

        var filePath = Path.Combine(weddingDir, "CombinedSongs.csv");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });
    }

    private static async Task GenerateCombinedSongsCsvAsync(
        Wedding wedding, List<WeddingDetail> pastDetails, string weddingDir)
    {
        var sb = new System.Text.StringBuilder();
        var weddingAndDate = $"{WeddingTitleHelper.Compute(wedding)}, {wedding.DateOfWedding:M/d/yyyy}";

        // ── Section 1: Current Wedding Songs ──────────────────────────────
        sb.AppendLine("CURRENT WEDDING SONGS");
        sb.AppendLine("Person Name,Role,Wedding And Date,Song Used");

        var detailsWithSongs = wedding.Details
            .Where(d => d.SongId.HasValue && d.Song != null)
            .OrderBy(d => d.RoleType);

        foreach (var d in detailsWithSongs)
        {
            var personName = d.Person?.FullName ?? string.Empty;
            sb.AppendLine($"{Csv(personName)},{Csv(RoleHelper.GetLabel(d.RoleType))},{Csv(weddingAndDate)},{Csv(d.Song!.Title)}");
        }

        // ── Section 2: Past Songs History ─────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("PAST SONGS HISTORY");
        sb.AppendLine("Person Name,Past Role,Past Wedding And Date,Song Used");

        var grouped = pastDetails.GroupBy(d => d.PersonId!.Value);
        foreach (var personGroup in grouped)
        {
            var person = personGroup.First().Person!;

            foreach (var d in personGroup.OrderByDescending(d => d.Wedding.DateOfWedding))
            {
                var pastWeddingAndDate = $"{WeddingTitleHelper.Compute(d.Wedding)}, {d.Wedding.DateOfWedding:M/d/yyyy}";
                var pastRole = RoleHelper.GetLabel(d.RoleType);

                if (d.SongId.HasValue && d.Song != null)
                    sb.AppendLine($"{Csv(person.FullName)},{Csv(pastRole)},{Csv(pastWeddingAndDate)},{Csv(d.Song.Title)}");
                else
                    sb.AppendLine($"{Csv(person.FullName)},{Csv(pastRole)},{Csv(pastWeddingAndDate)},(none assigned)");
            }
        }

        await File.WriteAllTextAsync(Path.Combine(weddingDir, "CombinedSongs.csv"), sb.ToString());
    }

    /// <summary>Wraps a value in quotes if it contains a comma, quote, or newline.</summary>
    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    public async Task<string> GetMasterPerformancePathAsync(int weddingId)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Details).ThenInclude(d => d.Person)
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var weddingDir = ResolveWeddingDir(wedding, weddingId);
        return Path.Combine(weddingDir, "Master_Performance.docx");
    }

    public async Task<string?> GetRoleSongPathAsync(int weddingId, RoleType roleType, int assignmentSlot = 1)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Details).ThenInclude(d => d.Person)
            .Include(w => w.Details).ThenInclude(d => d.Song)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var detail = wedding.Details.FirstOrDefault(d => d.RoleType == roleType && d.SongId.HasValue);
        if (detail?.Song == null) return null;

        var weddingDir = ResolveWeddingDir(wedding, weddingId);
        var songsFolder = Path.Combine(weddingDir, "Songs");

        var label = assignmentSlot > 1
            ? $"{RoleHelper.GetLabel(roleType)} (Slot {assignmentSlot})"
            : RoleHelper.GetLabel(roleType);

        var destFileName = SanitizeFileName($"{label}_{detail.Song.Title}") + ".docx";
        var destPath = Path.Combine(songsFolder, destFileName);

        return File.Exists(destPath) ? destPath : null;
    }

    public async Task<string> GenerateSongHistoryFileAsync(int weddingId)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Details).ThenInclude(d => d.Person)
            .Include(w => w.Details).ThenInclude(d => d.Song)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var personIds = wedding.Details
            .Where(d => d.PersonId.HasValue)
            .Select(d => d.PersonId!.Value)
            .Distinct()
            .ToList();

        var weddingDir = ResolveWeddingDir(wedding, weddingId);
        MarkWeddingDirOwnership(weddingDir, weddingId);

        var pastDetails = await QueryPastDetailsAsync(personIds, weddingId);
        var content = BuildHistoryText(wedding, pastDetails);

        var filePath = Path.Combine(weddingDir, "PastSongs.txt");
        await File.WriteAllTextAsync(filePath, content);
        return filePath;
    }

    private string ResolveWeddingDir(Wedding wedding, int weddingId)
    {
        var groomDetail = wedding.Details.FirstOrDefault(d => d.RoleType == RoleType.Groom);
        var brideDetail = wedding.Details.FirstOrDefault(d => d.RoleType == RoleType.Bride);
        var groomName = SanitizeFolderSegment(groomDetail?.Person?.LastName ?? "Groom");
        var brideName = SanitizeFolderSegment(brideDetail?.Person?.LastName ?? "Bride");

        var gregorianYear = wedding.DateOfWedding.ToString("yyyy", CultureInfo.InvariantCulture);
        var gregorianMonth = wedding.DateOfWedding.ToString("MMM", CultureInfo.InvariantCulture);
        var (hebrewDay, hebrewMonth, hebrewYear) = HebrewDateHelper.GetHebrewParts(wedding.DateOfWedding);
        var coupleFolder = SanitizeFolderSegment($"{brideName} - {groomName} - {hebrewDay} {hebrewMonth} {hebrewYear}");

        var baseDir = Path.Combine(_weddingOutputRoot, gregorianYear, gregorianMonth, coupleFolder);
        return ResolveWithCollisionSuffix(baseDir, weddingId);
    }

    /// <summary>
    /// Two different weddings can land on the same bride/groom/Hebrew-date folder name.
    /// Ownership is tracked via a ".wedding-id" marker file so repeat calls for the same
    /// wedding keep resolving to the same folder, while a genuine collision gets " (2)", " (3)", etc.
    /// Read-only: does not create anything on disk, so it's safe to call from path lookups
    /// that don't want side effects.
    /// </summary>
    private static string ResolveWithCollisionSuffix(string baseDir, int weddingId)
    {
        var candidate = baseDir;
        var suffix = 1;

        while (Directory.Exists(candidate) && !OwnsDir(candidate, weddingId))
        {
            suffix++;
            candidate = $"{baseDir} ({suffix})";
        }

        return candidate;
    }

    private static bool OwnsDir(string dir, int weddingId)
    {
        var markerPath = Path.Combine(dir, ".wedding-id");
        return File.Exists(markerPath) && File.ReadAllText(markerPath).Trim() == weddingId.ToString();
    }

    /// <summary>Creates the wedding's folder (if needed) and stamps it with the owning weddingId.</summary>
    private static void MarkWeddingDirOwnership(string weddingDir, int weddingId)
    {
        Directory.CreateDirectory(weddingDir);
        File.WriteAllText(Path.Combine(weddingDir, ".wedding-id"), weddingId.ToString());
    }

    private async Task<List<WeddingDetail>> QueryPastDetailsAsync(List<int> personIds, int excludeWeddingId)
    {
        return await _db.WeddingDetails
            .Include(d => d.Wedding).ThenInclude(w => w.Details).ThenInclude(d2 => d2.Person)
            .Include(d => d.Person)
            .Include(d => d.Song)
            .AsSplitQuery()
            .Where(d => d.PersonId.HasValue
                     && personIds.Contains(d.PersonId!.Value)
                     && d.WeddingId != excludeWeddingId
                     && d.RoleType != RoleType.WeddingItself)
            .OrderByDescending(d => d.Wedding.DateOfWedding)
            .ToListAsync();
    }

    private static string BuildHistoryText(Wedding wedding, List<WeddingDetail> pastDetails)
    {
        const int col1 = 16, col2 = 12, col3 = 32;
        const int sepWidth = 70;
        var sep = new string('=', sepWidth);
        var sb = new System.Text.StringBuilder();

        // ── Section 1: Current Wedding Song History ───────────────────────
        sb.AppendLine("CURRENT WEDDING SONG HISTORY");
        sb.AppendLine(sep);
        sb.AppendLine();

        sb.AppendLine($"{"Persons Name".PadRight(col1)}{"Role".PadRight(col2)}{"Wedding Name and Date".PadRight(col3)}Song Used");

        var currentNameAndDate = $"{WeddingTitleHelper.Compute(wedding)}, {wedding.DateOfWedding:yyyy-MM-dd}";
        var detailsWithSongs = wedding.Details
            .Where(d => d.SongId.HasValue && d.Song != null)
            .OrderBy(d => d.RoleType);

        foreach (var d in detailsWithSongs)
        {
            var name = d.Person?.FullName ?? string.Empty;
            var label = RoleHelper.GetLabel(d.RoleType);
            sb.AppendLine($"{name.PadRight(col1)}{label.PadRight(col2)}{currentNameAndDate.PadRight(col3)}{d.Song!.Title}");
        }

        sb.AppendLine();
        sb.AppendLine(sep);
        sb.AppendLine();

        // ── Section 2: Past Wedding Song History ──────────────────────────
        sb.AppendLine("PAST WEDDING SONG HISTORY");
        sb.AppendLine();

        sb.AppendLine($"{"Persons Name".PadRight(col1)}{"Past Role".PadRight(col2)}{"Past Wedding Name and Date".PadRight(col3)}Song Used");

        var grouped = pastDetails.GroupBy(d => d.PersonId!.Value);
        foreach (var personGroup in grouped)
        {
            var person = personGroup.First().Person!;
            foreach (var d in personGroup.OrderByDescending(d => d.Wedding.DateOfWedding))
            {
                var pastNameAndDate = $"{WeddingTitleHelper.Compute(d.Wedding)}, {d.Wedding.DateOfWedding:yyyy-MM-dd}";
                var pastRole = RoleHelper.GetLabel(d.RoleType);
                if (d.SongId.HasValue && d.Song != null)
                    sb.AppendLine($"{person.FullName.PadRight(col1)}{pastRole.PadRight(col2)}{pastNameAndDate.PadRight(col3)}{d.Song.Title}");
                else
                    sb.AppendLine($"{person.FullName.PadRight(col1)}{pastRole.PadRight(col2)}{pastNameAndDate.PadRight(col3)}(none assigned)");
            }
        }

        return sb.ToString();
    }

    private static string SanitizeFolderSegment(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) || c == '/' || c == '\\' ? '_' : c).ToArray()).Trim();
    }
}
