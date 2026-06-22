namespace WeddingOrchestrator.Api.DTOs.People;

public class PersonSummaryDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
}

public class PersonWeddingDto
{
    public int WeddingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsFinalized { get; set; }
    public string? SongTitle { get; set; }
}

public class PersonProfileDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;

    public PersonSummaryDto? Father { get; set; }
    public PersonSummaryDto? Mother { get; set; }
    public List<PersonSummaryDto> Siblings { get; set; } = new();

    public PersonSummaryDto? PaternalGrandfather { get; set; }
    public PersonSummaryDto? PaternalGrandmother { get; set; }
    public PersonSummaryDto? MaternalGrandfather { get; set; }
    public PersonSummaryDto? MaternalGrandmother { get; set; }

    public List<PersonWeddingDto> Weddings { get; set; } = new();
    public List<PersonNoteDto> Notes { get; set; } = new();
}
