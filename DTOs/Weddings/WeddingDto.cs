namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class WeddingDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime DateOfWedding { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Location { get; set; }
    public bool IsFinalized { get; set; }
    public List<WeddingRoleDto> Roles { get; set; } = new();
    public ConflictReportDto? ConflictReport { get; set; }
}
