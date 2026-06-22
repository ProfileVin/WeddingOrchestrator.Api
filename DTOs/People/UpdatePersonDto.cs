using System.ComponentModel.DataAnnotations;
using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.DTOs.People;

public class UpdatePersonDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    public Gender Gender { get; set; }
    public int? FatherId { get; set; }
    public int? MotherId { get; set; }

    [MaxLength(200)]
    public string? FamilyGroup { get; set; }
}
