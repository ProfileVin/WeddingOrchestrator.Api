namespace WeddingOrchestrator.Api.DTOs.Songs;

public class UpdateSongDto
{
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
}
