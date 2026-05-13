using WeddingOrchestrator.Api.DTOs.Songs;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface ISongService
{
    Task<List<SongCategoryDto>> GetAllCategoriesWithSongsAsync();
    Task<SongCategoryDto> CreateCategoryAsync(CreateSongCategoryDto dto);
    Task<List<SongDto>> GetAllSongsAsync();
    Task<SongDto> GetByIdAsync(int id);
    Task<SongDto> UploadSongAsync(IFormFile file, int categoryId);
    Task<SongDto> CreateBlankSongAsync(CreateSongDto dto);
    Task<SongDto> UpdateSongAsync(int id, UpdateSongDto dto);
    Task DeleteSongAsync(int id);
    Task<(Stream stream, string fileName, string contentType)> DownloadSongAsync(int id);
    Task OpenInWordAsync(int id);
    Task SyncFileMetadataAsync();
}
