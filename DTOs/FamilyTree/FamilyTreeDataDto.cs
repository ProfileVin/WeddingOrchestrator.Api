namespace WeddingOrchestrator.Api.DTOs.FamilyTree;

public class FamilySummaryDto
{
    public string LastName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
}

public class FamilySummariesResponseDto
{
    public List<FamilySummaryDto> Families { get; set; } = new();
    public int TotalPeople { get; set; }
}

public class FamilyTreeDataDto
{
    public List<FamilyPersonDto> People { get; set; } = new();
    public List<PersonRelationshipDto> Relationships { get; set; } = new();
}

public class FamilyPersonDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
}
