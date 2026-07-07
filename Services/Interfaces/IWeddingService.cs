using WeddingOrchestrator.Api.DTOs.Weddings;
using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IWeddingService
{
    Task<List<WeddingListItemDto>> GetAllAsync();
    Task<List<WeddingListItemDto>> CheckAvailabilityAsync(DateTime date, TimeOnly? startTime, TimeOnly? endTime);
    Task<WeddingDto> GetByIdAsync(int id);
    Task<WeddingDto> CreateAsync(CreateWeddingDto dto);
    Task<WeddingDto> UpdateDetailsAsync(int id, UpdateWeddingDetailsDto dto);
    Task<WeddingDto> UpdateRolesAsync(int id, WeddingFamilyTreeDto dto);
    Task<WeddingDto> AssignSongsAsync(int id, AssignSongsDto dto);
    Task<WeddingDto> FinalizeAsync(int id);
    Task<WeddingDto> UnfinalizeAsync(int id);
    Task DeleteAsync(int id);
    Task<(string roleLabel, string filePath, string songTitle)?> GetRoleSongExportDataAsync(int weddingId, RoleType roleType);
    Task<List<(string roleLabel, string personName, string songTitle, string filePath)>> GetCombinedExportDataAsync(int weddingId);
    Task<WeddingDto> LinkOtherRelationsAsync(int weddingId, List<LinkOtherRelationDto> dto);
    Task DeleteOtherRelationAsync(int weddingId, int personId);
    Task<WeddingDto> UpdateDetailNoteAsync(int weddingId, UpdateDetailNoteDto dto);
    Task<WeddingDto> UpdateWeddingSongIntrosAsync(int weddingId, UpdateWeddingSongIntrosDto dto);
}
