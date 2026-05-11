namespace WeddingOrchestrator.Api.DTOs.People;

public class CreatePersonDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int? FatherId { get; set; }
    public int? MotherId { get; set; }
}
