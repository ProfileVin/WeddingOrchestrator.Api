using System.ComponentModel.DataAnnotations;

namespace WeddingOrchestrator.Api.DTOs.People;

public class CreatePersonDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    public int? FatherId { get; set; }
    public int? MotherId { get; set; }
}
