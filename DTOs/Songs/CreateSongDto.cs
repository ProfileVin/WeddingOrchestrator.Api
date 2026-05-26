using System.ComponentModel.DataAnnotations;

namespace WeddingOrchestrator.Api.DTOs.Songs;

public class CreateSongDto
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}
