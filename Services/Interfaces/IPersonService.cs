using WeddingOrchestrator.Api.DTOs.People;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IPersonService
{
    Task<List<PersonDto>> GetAllAsync();
    Task<List<PersonDto>> SearchAsync(string query);
    Task<PersonDto> GetByIdAsync(int id);
    Task<PersonDto> CreateAsync(CreatePersonDto dto);
    Task<PersonDto> UpdateAsync(int id, UpdatePersonDto dto);
    Task DeleteAsync(int id);
}
