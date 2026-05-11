namespace WeddingOrchestrator.Api.DTOs.Songs;

public class SongDto
{
    public int Id { get; set; }
    public int SequenceNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}
