namespace WeddingOrchestrator.Api.DTOs.Songs;

public class SongCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public List<SongDto> Songs { get; set; } = new();
}
