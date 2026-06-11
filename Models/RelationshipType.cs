namespace WeddingOrchestrator.Api.Models;

public class RelationshipType
{
    public int Id { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Category { get; set; } = "DIRECT";
    public int GenerationDelta { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PersonRelationship> Relationships { get; set; } = new List<PersonRelationship>();
}
