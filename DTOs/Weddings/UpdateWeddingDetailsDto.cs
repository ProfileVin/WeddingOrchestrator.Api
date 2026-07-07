namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class UpdateWeddingDetailsDto
{
    public DateTime DateOfWedding { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Location { get; set; }
}
