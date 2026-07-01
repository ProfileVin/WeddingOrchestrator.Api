using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Models;

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender Gender { get; set; }

    public string? FamilyGroup { get; set; }

    public int? FatherId { get; set; }
    public int? MotherId { get; set; }

    public Person? Father { get; set; }
    public Person? Mother { get; set; }

    public ICollection<WeddingDetail> WeddingDetails { get; set; } = new List<WeddingDetail>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
