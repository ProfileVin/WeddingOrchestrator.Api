using WeddingOrchestrator.Api.DTOs.Weddings;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IWeddingService
{
    Task<List<WeddingListItemDto>> GetAllAsync();
    Task<WeddingDto> GetByIdAsync(int id);
    Task<WeddingDto> CreateAsync(CreateWeddingDto dto);
    Task<WeddingDto> UpdateRolesAsync(int id, WeddingFamilyTreeDto dto);
    Task<WeddingDto> AssignSongsAsync(int id, AssignSongsDto dto);
    Task<WeddingDto> FinalizeAsync(int id);
    Task DeleteAsync(int id);
}
