using WeddingOrchestrator.Api.DTOs.People;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IPersonNoteService
{
    Task<List<PersonNoteDto>> GetByPersonAsync(int personId);
    Task<PersonNoteDto> CreateAsync(int personId, CreatePersonNoteDto dto);
    Task<PersonNoteDto> UpdateAsync(int noteId, UpdatePersonNoteDto dto);
    Task DeleteAsync(int noteId);
}
