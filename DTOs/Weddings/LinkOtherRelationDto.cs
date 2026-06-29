namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class LinkOtherRelationDto
{
    public int? PersonId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int RelationshipTypeId { get; set; }
    public string? WeddingSide { get; set; }
    public int? RelatedPersonId { get; set; }
}
