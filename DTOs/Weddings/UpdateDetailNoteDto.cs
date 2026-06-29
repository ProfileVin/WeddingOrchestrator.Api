using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class UpdateDetailNoteDto
{
    public RoleType RoleType { get; set; }
    public int? PersonId { get; set; }
    public string? Note { get; set; }
}
