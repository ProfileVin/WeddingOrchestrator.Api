using WeddingOrchestrator.Api.DTOs.FamilyTree;
using WeddingOrchestrator.Api.DTOs.People;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IFamilyTreeService
{
    Task<FamilySummariesResponseDto> GetFamilySummariesAsync();
    Task<List<RelationshipTypeDto>> GetRelationshipTypesAsync();
    Task<FamilyTreeDataDto> GetFamilyTreeByLastNameAsync(string lastName);
    Task<List<PersonRelationshipDto>> GetPersonRelationshipsAsync(int personId);
    Task<PersonRelationshipDto> CreateRelationshipAsync(CreateRelationshipDto dto);
    Task DeleteRelationshipAsync(int id);
    Task<PersonDto> AddFamilyMemberAsync(AddFamilyMemberDto dto);
    Task<PersonDto> AddWeddingRelativeAsync(AddWeddingRelativeDto dto);
    Task BuildRelationshipsForExistingPersonAsync(int personId, int relatedPersonId, string typeCode, int weddingId);
}
