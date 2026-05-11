namespace WeddingOrchestrator.Api.Models;

public class SongCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public ICollection<Song> Songs { get; set; } = new List<Song>();
}
