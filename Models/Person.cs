namespace WeddingOrchestrator.Api.Models;

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public int? FatherId { get; set; }
    public int? MotherId { get; set; }

    public Person? Father { get; set; }
    public Person? Mother { get; set; }

    public ICollection<WeddingRole> WeddingRoles { get; set; } = new List<WeddingRole>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
