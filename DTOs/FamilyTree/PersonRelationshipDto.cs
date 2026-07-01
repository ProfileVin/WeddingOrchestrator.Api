namespace WeddingOrchestrator.Api.DTOs.FamilyTree;

public class PersonRelationshipDto
{
    public int Id { get; set; }
    public int FromPersonId { get; set; }
    public string FromPersonName { get; set; } = string.Empty;
    public int ToPersonId { get; set; }
    public string ToPersonName { get; set; } = string.Empty;
    public int RelationshipTypeId { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateRelationshipDto
{
    public int FromPersonId { get; set; }
    public int ToPersonId { get; set; }
    public int RelationshipTypeId { get; set; }
}
