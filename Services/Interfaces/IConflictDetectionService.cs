using WeddingOrchestrator.Api.DTOs.Weddings;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IConflictDetectionService
{
    Task<ConflictReportDto> GetConflictReportAsync(int weddingId);
    Task<HashSet<int>> GetForbiddenSongIdsAsync(int weddingId);
}
