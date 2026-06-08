namespace WeddingOrchestrator.Api.DTOs.People;

public class PersonDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string? FamilyGroup { get; set; }
    public int WeddingCount { get; set; }
    public string? LastNote { get; set; }

    public int? FatherId { get; set; }
    public string? FatherName { get; set; }
    public int? MotherId { get; set; }
    public string? MotherName { get; set; }

    // Grandparents — resolved 2 levels up for auto-fill
    public int? PaternalGrandfatherId { get; set; }
    public string? PaternalGrandfatherName { get; set; }
    public int? PaternalGrandmotherId { get; set; }
    public string? PaternalGrandmotherName { get; set; }
    public int? MaternalGrandfatherId { get; set; }
    public string? MaternalGrandfatherName { get; set; }
    public int? MaternalGrandmotherId { get; set; }
    public string? MaternalGrandmotherName { get; set; }
}
