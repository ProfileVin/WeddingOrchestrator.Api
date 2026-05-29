using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.Infrastructure;
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

        var groomRole = wedding.Roles.FirstOrDefault(r => r.RoleType == RoleType.Groom);
        var brideRole = wedding.Roles.FirstOrDefault(r => r.RoleType == RoleType.Bride);
        var groomName = SanitizeFolderSegment(groomRole?.Person?.LastName ?? "Groom");
        var brideName = SanitizeFolderSegment(brideRole?.Person?.LastName ?? "Bride");

        var dateSegment = wedding.DateOfWedding.ToString("yyyy-MM-dd");
        var coupleFolder = $"{groomName}-{brideName}";
        var songsFolder = Path.Combine(_weddingOutputRoot, dateSegment, weddingId.ToString(), coupleFolder, "Songs");

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
