namespace WeddingOrchestrator.Api.Models;

public class PersonRelationship
{
    public int Id { get; set; }
    public int FromPersonId { get; set; }
    public int ToPersonId { get; set; }
    public int RelationshipTypeId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Person FromPerson { get; set; } = null!;
    public Person ToPerson { get; set; } = null!;
    public RelationshipType RelationshipType { get; set; } = null!;
}
