using System.ComponentModel.DataAnnotations;

namespace WeddingOrchestrator.Api.DTOs.FamilyTree;

public class AddFamilyMemberDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string RoleCode { get; set; } = string.Empty;

    public int? FatherId { get; set; }
    public int? MotherId { get; set; }
}