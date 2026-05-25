using WeddingOrchestrator.Api.DTOs.Weddings;
using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IWeddingService
{
    Task<List<WeddingListItemDto>> GetAllAsync();
    Task<List<WeddingListItemDto>> CheckAvailabilityAsync(DateTime date, TimeOnly? startTime, TimeOnly? endTime);
    Task<WeddingDto> GetByIdAsync(int id);
    Task<WeddingDto> CreateAsync(CreateWeddingDto dto);
    Task<WeddingDto> UpdateRolesAsync(int id, WeddingFamilyTreeDto dto);
    Task<WeddingDto> AssignSongsAsync(int id, AssignSongsDto dto);
    Task<WeddingDto> FinalizeAsync(int id);
    Task<WeddingDto> UnfinalizeAsync(int id);
    Task DeleteAsync(int id);
    Task<(string roleLabel, string filePath, string songTitle)?> GetRoleSongExportDataAsync(int weddingId, RoleType roleType);
    Task<List<(string roleLabel, string songTitle, string filePath)>> GetCombinedExportDataAsync(int weddingId);
}
