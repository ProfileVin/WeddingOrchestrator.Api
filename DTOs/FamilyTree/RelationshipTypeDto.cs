namespace WeddingOrchestrator.Api.DTOs.FamilyTree;

public class RelationshipTypeDto
{
    public int Id { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int GenerationDelta { get; set; }
}
