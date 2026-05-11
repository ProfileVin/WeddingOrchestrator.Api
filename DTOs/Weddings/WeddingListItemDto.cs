namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class WeddingListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime DateOfWedding { get; set; }
    public string? Location { get; set; }
    public bool IsFinalized { get; set; }
    public string GroomName { get; set; } = string.Empty;
    public string BrideName { get; set; } = string.Empty;
}
