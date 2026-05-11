using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.DTOs.Songs;
using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class SongService : ISongService
{
    private readonly AppDbContext _db;
    private readonly IDocxService _docx;
    private readonly string _storageRoot;

    public SongService(AppDbContext db, IDocxService docx, IWebHostEnvironment env)
    {
        _db = db;
        _docx = docx;
        _storageRoot = Path.Combine(env.ContentRootPath, "Storage", "Songs");
    }

    public async Task<List<SongCategoryDto>> GetAllCategoriesWithSongsAsync()
    {
        var categories = await _db.SongCategories
            .Include(c => c.Songs)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        return categories.Select(MapCategoryDto).ToList();
    }

    public async Task<List<SongDto>> GetAllSongsAsync()
    {
        var songs = await _db.Songs
            .Include(s => s.Category)
            .OrderBy(s => s.Category.DisplayOrder)
            .ThenBy(s => s.Title)
            .ToListAsync();

        return songs
            .GroupBy(s => s.SongCategoryId)
            .SelectMany(g => g.Select((s, i) => MapSongDto(s, i + 1)))
            .ToList();
    }

    public async Task<SongDto> GetByIdAsync(int id)
    {
        var song = await _db.Songs.Include(s => s.Category).FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException($"Song {id} not found.");
        return MapSongDto(song);
    }

    public async Task<SongDto> UploadSongAsync(IFormFile file, int categoryId)
    {
        var category = await _db.SongCategories.FindAsync(categoryId)
            ?? throw new KeyNotFoundException($"Category {categoryId} not found.");

        var dir = GetCategoryDir(category.Name);
        Directory.CreateDirectory(dir);

        var title = Path.GetFileNameWithoutExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}_{SanitizeFileName(title)}.docx";
        var absolutePath = Path.Combine(dir, fileName);

        await using (var fs = new FileStream(absolutePath, FileMode.Create))
            await file.CopyToAsync(fs);

        var info = new FileInfo(absolutePath);
        var song = new Song
        {
            Title = title,
            SongCategoryId = categoryId,
            RelativeFilePath = Path.GetRelativePath(_storageRoot, absolutePath),
            FileSizeBytes = info.Length,
            LastUpdatedUtc = info.LastWriteTimeUtc,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Songs.Add(song);
        await _db.SaveChangesAsync();
        song.Category = category;
        return MapSongDto(song);
    }

    public async Task<SongDto> CreateBlankSongAsync(CreateSongDto dto)
    {
        var category = await _db.SongCategories.FindAsync(dto.CategoryId)
            ?? throw new KeyNotFoundException($"Category {dto.CategoryId} not found.");

        var dir = GetCategoryDir(category.Name);
        Directory.CreateDirectory(dir);

        var absolutePath = _docx.CreateBlankDocx(dto.Title, dir);
        var info = new FileInfo(absolutePath);

        var song = new Song
        {
            Title = dto.Title,
            SongCategoryId = dto.CategoryId,
            RelativeFilePath = Path.GetRelativePath(_storageRoot, absolutePath),
            FileSizeBytes = info.Length,
            LastUpdatedUtc = info.LastWriteTimeUtc,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Songs.Add(song);
        await _db.SaveChangesAsync();
        song.Category = category;
        return MapSongDto(song);
    }

    public async Task<SongDto> UpdateSongAsync(int id, UpdateSongDto dto)
    {
        var song = await _db.Songs.Include(s => s.Category).FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException($"Song {id} not found.");

        song.Title = dto.Title;

        if (song.SongCategoryId != dto.CategoryId)
        {
            var newCategory = await _db.SongCategories.FindAsync(dto.CategoryId)
                ?? throw new KeyNotFoundException($"Category {dto.CategoryId} not found.");
            song.SongCategoryId = dto.CategoryId;
            song.Category = newCategory;
        }

        await _db.SaveChangesAsync();
        return MapSongDto(song);
    }

    public async Task DeleteSongAsync(int id)
    {
        var song = await _db.Songs.FindAsync(id)
            ?? throw new KeyNotFoundException($"Song {id} not found.");

        var absolutePath = Path.Combine(_storageRoot, song.RelativeFilePath);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        _db.Songs.Remove(song);
        await _db.SaveChangesAsync();
    }

    public async Task<(Stream stream, string fileName, string contentType)> DownloadSongAsync(int id)
    {
        var song = await _db.Songs.FindAsync(id)
            ?? throw new KeyNotFoundException($"Song {id} not found.");

        var absolutePath = Path.Combine(_storageRoot, song.RelativeFilePath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("File not found on disk.");

        var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, $"{song.Title}.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    public async Task OpenInWordAsync(int id)
    {
        var song = await _db.Songs.FindAsync(id)
            ?? throw new KeyNotFoundException($"Song {id} not found.");

        var absolutePath = Path.Combine(_storageRoot, song.RelativeFilePath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("File not found on disk.");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = absolutePath,
            UseShellExecute = true
        });
    }

    public async Task SyncFileMetadataAsync()
    {
        var songs = await _db.Songs.ToListAsync();
        foreach (var song in songs)
        {
            var absolutePath = Path.Combine(_storageRoot, song.RelativeFilePath);
            if (!File.Exists(absolutePath)) continue;
            var info = new FileInfo(absolutePath);
            song.FileSizeBytes = info.Length;
            song.LastUpdatedUtc = info.LastWriteTimeUtc;
        }
        await _db.SaveChangesAsync();
    }

    private string GetCategoryDir(string categoryName) =>
        Path.Combine(_storageRoot, SanitizeFileName(categoryName));

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");

    private static SongDto MapSongDto(Song s, int sequenceNumber = 0) => new()
    {
        Id = s.Id,
        SequenceNumber = sequenceNumber,
        Title = s.Title,
        CategoryId = s.SongCategoryId,
        CategoryName = s.Category.Name,
        FileSizeBytes = s.FileSizeBytes,
        LastUpdatedUtc = s.LastUpdatedUtc,
        CreatedUtc = s.CreatedUtc
    };

    private static SongCategoryDto MapCategoryDto(SongCategory c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        DisplayOrder = c.DisplayOrder,
        Songs = c.Songs.OrderBy(s => s.Title).Select((s, i) => MapSongDto(s, i + 1)).ToList()
    };
}
