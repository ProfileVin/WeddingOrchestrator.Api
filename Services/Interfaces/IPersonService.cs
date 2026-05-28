using WeddingOrchestrator.Api.DTOs.People;
using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IPersonService
{
    Task<List<PersonDto>> GetAllAsync();
    Task<List<PersonDto>> SearchAsync(string query, RoleType? roleType = null);
    Task<PersonDto> GetByIdAsync(int id);
    Task<PersonProfileDto> GetProfileAsync(int id);
    Task<PersonDto> CreateAsync(CreatePersonDto dto);
    Task<PersonDto> UpdateAsync(int id, UpdatePersonDto dto);
    Task DeleteAsync(int id);
}
