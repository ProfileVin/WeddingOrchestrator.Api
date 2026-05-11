namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class CreateWeddingDto
{
    public DateTime DateOfWedding { get; set; }
    public string? Location { get; set; }
    public string? GroomName { get; set; }
    public string? BrideName { get; set; }
    public int? WeddingIntroSongId { get; set; }
}
