using System.ComponentModel.DataAnnotations;

namespace WeddingOrchestrator.Api.DTOs.Songs;

public class CreateSongCategoryDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
