using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Models;

public class WeddingDetail
{
    public int Id { get; set; }
    public int WeddingId { get; set; }
    public int? PersonId { get; set; }
    public RoleType RoleType { get; set; }
    public int? InWeddingRelationTypeId { get; set; }
    public string? WeddingSide { get; set; }
    public int? SongId { get; set; }
    public string? Note { get; set; }
    public int? RelatedToPersonId { get; set; }

    public Wedding Wedding { get; set; } = null!;
    public Person? Person { get; set; }
    public Song? Song { get; set; }
    public RelationshipType? WeddingRelationType { get; set; }
    public Person? RelatedToPerson { get; set; }
}
