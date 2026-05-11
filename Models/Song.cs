namespace WeddingOrchestrator.Api.Models;

public class Song
{
    public int Id { get; set; }
    public int SongCategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RelativeFilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }

    public SongCategory Category { get; set; } = null!;
    public ICollection<WeddingRoleSongAssignment> Assignments { get; set; } = new List<WeddingRoleSongAssignment>();
}
