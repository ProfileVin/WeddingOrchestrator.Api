namespace WeddingOrchestrator.Api.DTOs.Search;

public class SearchPersonResult
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public class SearchSongResult
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public class SearchWeddingResult
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsFinalized { get; set; }
}

public class GlobalSearchResultDto
{
    public List<SearchPersonResult> People { get; set; } = new();
    public List<SearchSongResult> Songs { get; set; } = new();
    public List<SearchWeddingResult> Weddings { get; set; } = new();
}
