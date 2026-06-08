using System.ComponentModel.DataAnnotations;

namespace WeddingOrchestrator.Api.DTOs.People;

public class PersonNoteDto
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public int? WeddingId { get; set; }
    public string? WeddingTitle { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreatePersonNoteDto
{
    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public int? WeddingId { get; set; }
}

public class UpdatePersonNoteDto
{
    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}
