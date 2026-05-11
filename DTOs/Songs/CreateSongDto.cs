namespace WeddingOrchestrator.Api.DTOs.Songs;

public class CreateSongDto
{
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
}
