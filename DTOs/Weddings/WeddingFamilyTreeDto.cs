using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class WeddingFamilyTreeDto
{
    public List<RoleSlotDto> Roles { get; set; } = new();
    public string? Notes { get; set; }
}

public class RoleSlotDto
{
    public RoleType RoleType { get; set; }
    public int? PersonId { get; set; }
    public string? FreeTextName { get; set; }
    public int? RelationshipTypeId { get; set; }
}
