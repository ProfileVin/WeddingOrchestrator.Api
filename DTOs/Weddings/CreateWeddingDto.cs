namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class CreateWeddingDto
{
    public DateTime DateOfWedding { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Location { get; set; }
    public string? GroomName { get; set; }
    public string? GroomFirstName { get; set; }
    public string? GroomLastName { get; set; }
    public string? BrideName { get; set; }
    public string? BrideFirstName { get; set; }
    public string? BrideLastName { get; set; }
    public int? WeddingIntroSongId { get; set; }
    public string? Notes { get; set; }
}
