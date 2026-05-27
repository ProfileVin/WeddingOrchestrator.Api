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
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .Include(w => w.Roles).ThenInclude(r => r.SongAssignments).ThenInclude(a => a.Song)
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var weddingDir  = ResolveWeddingDir(wedding, weddingId);
        var songsFolder = Path.Combine(weddingDir, "Songs");
        Directory.CreateDirectory(songsFolder);

        var assignments = wedding.Roles
            .SelectMany(r => r.SongAssignments.Select(a => new
            {
                RoleLabel = RoleHelper.GetLabel(r.RoleType),
                a.AssignmentSlot,
                a.Song.Title,
                a.Song.RelativeFilePath
            }))
            .ToList();

        var expectedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in assignments)
        {
            var sourcePath = Path.Combine(_songStorageRoot, a.RelativeFilePath);
            if (!File.Exists(sourcePath)) continue;

            var label = a.AssignmentSlot > 1
                ? $"{a.RoleLabel} (Slot {a.AssignmentSlot})"
                : a.RoleLabel;

            var destFileName = SanitizeFileName($"{label}_{a.Title}") + ".docx";
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
        var personIds = wedding.Roles
            .Where(r => r.PersonId.HasValue)
            .Select(r => r.PersonId!.Value)
            .Distinct()
            .ToList();
        var pastRoles = await QueryPastRolesAsync(personIds, weddingId);
        await GenerateCombinedSongsCsvAsync(wedding, pastRoles, weddingDir);
    }

    public async Task OpenCombinedSongsTxtAsync(int weddingId)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .Include(w => w.Roles).ThenInclude(r => r.SongAssignments).ThenInclude(a => a.Song)
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var weddingDir = ResolveWeddingDir(wedding, weddingId);
        Directory.CreateDirectory(weddingDir);

        var personIds = wedding.Roles
            .Where(r => r.PersonId.HasValue)
            .Select(r => r.PersonId!.Value)
            .Distinct()
            .ToList();
        var pastRoles = await QueryPastRolesAsync(personIds, weddingId);

        // Always regenerate so the file is never stale or missing
        await GenerateCombinedSongsCsvAsync(wedding, pastRoles, weddingDir);

        var filePath = Path.Combine(weddingDir, "CombinedSongs.csv");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });
    }

    private static async Task GenerateCombinedSongsCsvAsync(
        Wedding wedding, List<WeddingRole> pastRoles, string weddingDir)
    {
        var sb = new System.Text.StringBuilder();
        var weddingAndDate = $"{WeddingTitleHelper.Compute(wedding)}, {wedding.DateOfWedding:M/d/yyyy}";

        // ── Section 1: Current Wedding Songs ──────────────────────────────
        sb.AppendLine("CURRENT WEDDING SONGS");
        sb.AppendLine("Person Name,Role,Wedding And Date,Song Used");

        var rolesWithSongs = wedding.Roles
            .Where(r => r.SongAssignments.Count > 0)
            .OrderBy(r => r.RoleType);

        foreach (var role in rolesWithSongs)
        {
            var personName = role.Person?.FullName ?? string.Empty;
            foreach (var sa in role.SongAssignments.OrderBy(a => a.AssignmentSlot))
                sb.AppendLine($"{Csv(personName)},{Csv(RoleHelper.GetLabel(role.RoleType))},{Csv(weddingAndDate)},{Csv(sa.Song.Title)}");
        }

        // ── Section 2: Past Songs History ─────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("PAST SONGS HISTORY");
        sb.AppendLine("Person Name,Past Role,Past Wedding And Date,Song Used");

        var grouped = pastRoles.GroupBy(r => r.PersonId!.Value);
        foreach (var personGroup in grouped)
        {
            var person = personGroup.First().Person!;

            foreach (var role in personGroup.OrderByDescending(r => r.Wedding.DateOfWedding))
            {
                var pastWeddingAndDate = $"{WeddingTitleHelper.Compute(role.Wedding)}, {role.Wedding.DateOfWedding:M/d/yyyy}";
                var pastRole = RoleHelper.GetLabel(role.RoleType);

                if (role.SongAssignments.Count > 0)
                {
                    foreach (var sa in role.SongAssignments.OrderBy(a => a.AssignmentSlot))
                        sb.AppendLine($"{Csv(person.FullName)},{Csv(pastRole)},{Csv(pastWeddingAndDate)},{Csv(sa.Song.Title)}");
                }
                else
                {
                    sb.AppendLine($"{Csv(person.FullName)},{Csv(pastRole)},{Csv(pastWeddingAndDate)},(none assigned)");
                }
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

    public async Task<string?> GetRoleSongPathAsync(int weddingId, RoleType roleType, int assignmentSlot = 1)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .Include(w => w.Roles).ThenInclude(r => r.SongAssignments).ThenInclude(a => a.Song)
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var role = wedding.Roles.FirstOrDefault(r => r.RoleType == roleType);
        if (role == null) return null;

        var assignment = role.SongAssignments.FirstOrDefault(a => a.AssignmentSlot == assignmentSlot);
        if (assignment == null) return null;

        var groomRole = wedding.Roles.FirstOrDefault(r => r.RoleType == RoleType.Groom);
        var brideRole = wedding.Roles.FirstOrDefault(r => r.RoleType == RoleType.Bride);
        var groomName = SanitizeFolderSegment(groomRole?.Person?.LastName ?? "Groom");
        var brideName = SanitizeFolderSegment(brideRole?.Person?.LastName ?? "Bride");

        var dateSegment = wedding.DateOfWedding.ToString("yyyy-MM-dd");
        var coupleFolder = $"{groomName}-{brideName}";
        var songsFolder = Path.Combine(_weddingOutputRoot, dateSegment, weddingId.ToString(), coupleFolder, "Songs");

        var label = assignmentSlot > 1
            ? $"{RoleHelper.GetLabel(roleType)} (Slot {assignmentSlot})"
            : RoleHelper.GetLabel(roleType);

        var destFileName = SanitizeFileName($"{label}_{assignment.Song.Title}") + ".docx";
        var destPath = Path.Combine(songsFolder, destFileName);

        return File.Exists(destPath) ? destPath : null;
    }

    public async Task<string> GenerateSongHistoryFileAsync(int weddingId)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.Person)
            .FirstOrDefaultAsync(w => w.Id == weddingId)
            ?? throw new KeyNotFoundException($"Wedding {weddingId} not found.");

        var personIds = wedding.Roles
            .Where(r => r.PersonId.HasValue)
            .Select(r => r.PersonId!.Value)
            .Distinct()
            .ToList();

        var weddingDir = ResolveWeddingDir(wedding, weddingId);
        Directory.CreateDirectory(weddingDir);

        var pastRoles = await QueryPastRolesAsync(personIds, weddingId);
        var content   = BuildHistoryText(wedding, pastRoles);

        var filePath = Path.Combine(weddingDir, "PastSongs.txt");
        await File.WriteAllTextAsync(filePath, content);
        return filePath;
    }

    private string ResolveWeddingDir(Wedding wedding, int weddingId)
    {
        var groomRole = wedding.Roles.FirstOrDefault(r => r.RoleType == RoleType.Groom);
        var brideRole = wedding.Roles.FirstOrDefault(r => r.RoleType == RoleType.Bride);
        var groomName = SanitizeFolderSegment(groomRole?.Person?.LastName ?? "Groom");
        var brideName = SanitizeFolderSegment(brideRole?.Person?.LastName ?? "Bride");
        var dateSegment = wedding.DateOfWedding.ToString("yyyy-MM-dd");
        return Path.Combine(_weddingOutputRoot, dateSegment, weddingId.ToString(), $"{groomName}-{brideName}");
    }

    private async Task<List<WeddingRole>> QueryPastRolesAsync(List<int> personIds, int excludeWeddingId)
    {
        return await _db.WeddingRoles
            .Include(r => r.Wedding).ThenInclude(w => w.Roles).ThenInclude(r2 => r2.Person)
            .Include(r => r.Person)
            .Include(r => r.SongAssignments).ThenInclude(a => a.Song)
            .Where(r => r.PersonId.HasValue
                     && personIds.Contains(r.PersonId!.Value)
                     && r.WeddingId != excludeWeddingId)
            .OrderByDescending(r => r.Wedding.DateOfWedding)
            .ToListAsync();
    }

    private static string BuildHistoryText(Wedding wedding, List<WeddingRole> pastRoles)
    {
        const int width = 52;
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("PAST SONG HISTORY");
        sb.AppendLine(new string('=', width));
        sb.AppendLine($"Wedding:   {WeddingTitleHelper.Compute(wedding)}");
        sb.AppendLine($"Date:      {wedding.DateOfWedding:yyyy-MM-dd}");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        var grouped = pastRoles.GroupBy(r => r.PersonId!.Value).ToList();

        if (grouped.Count == 0)
            sb.AppendLine("No past song history found for the people in this wedding.");
        else
            foreach (var personGroup in grouped)
                AppendPersonSection(sb, personGroup, wedding, width);

        sb.AppendLine(new string('=', width));
        return sb.ToString();
    }

    private static void AppendPersonSection(
        System.Text.StringBuilder sb,
        IGrouping<int, WeddingRole> personGroup,
        Wedding wedding,
        int width)
    {
        var person = personGroup.First().Person!;
        var currentRole = wedding.Roles.FirstOrDefault(r => r.PersonId == personGroup.Key);
        var currentRoleLabel = currentRole != null ? RoleHelper.GetLabel(currentRole.RoleType) : "—";

        sb.AppendLine(new string('-', width));
        sb.AppendLine($"Person:              {person.FullName}");
        sb.AppendLine($"Role (this wedding): {currentRoleLabel}");
        sb.AppendLine();

        foreach (var role in personGroup.OrderByDescending(r => r.Wedding.DateOfWedding))
            AppendRoleEntry(sb, role);
    }

    private static void AppendRoleEntry(System.Text.StringBuilder sb, WeddingRole role)
    {
        var pastTitle = WeddingTitleHelper.Compute(role.Wedding);
        sb.AppendLine($"  Past Wedding: {pastTitle} ({role.Wedding.DateOfWedding:yyyy-MM-dd})");
        sb.AppendLine($"  Role:         {RoleHelper.GetLabel(role.RoleType)}");

        var songs = role.SongAssignments.OrderBy(a => a.AssignmentSlot).ToList();
        if (songs.Count > 0)
        {
            foreach (var sa in songs)
            {
                var slotLabel = sa.AssignmentSlot > 1 ? $" (Slot {sa.AssignmentSlot})" : "";
                sb.AppendLine($"  Song{slotLabel}:         {sa.Song.Title}");
            }
        }
        else
        {
            sb.AppendLine("  Song:         (none assigned)");
        }

        sb.AppendLine();
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
