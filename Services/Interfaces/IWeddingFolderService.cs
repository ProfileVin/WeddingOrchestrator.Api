using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IWeddingFolderService
{
    Task SyncFolderAsync(int weddingId);
    Task<string?> GetRoleSongPathAsync(int weddingId, RoleType roleType, int assignmentSlot = 1);
    Task<string> GenerateSongHistoryFileAsync(int weddingId);
    Task OpenCombinedSongsTxtAsync(int weddingId);
}
