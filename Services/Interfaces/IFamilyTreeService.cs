using WeddingOrchestrator.Api.DTOs.FamilyTree;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IFamilyTreeService
{
    Task<List<RelationshipTypeDto>> GetRelationshipTypesAsync();
    Task<FamilyTreeDataDto> GetFamilyTreeByLastNameAsync(string lastName);
    Task<List<PersonRelationshipDto>> GetPersonRelationshipsAsync(int personId);
    Task<PersonRelationshipDto> CreateRelationshipAsync(CreateRelationshipDto dto);
    Task DeleteRelationshipAsync(int id);
}
