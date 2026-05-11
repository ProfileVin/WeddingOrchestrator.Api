namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class ConflictReportDto
{
    public List<ConflictingWeddingDto> ConflictingWeddings { get; set; } = new();
    public List<int> ForbiddenSongIds { get; set; } = new();
}

public class ConflictingWeddingDto
{
    public int WeddingId { get; set; }
    public string WeddingTitle { get; set; } = string.Empty;
    public List<SharedPersonDto> SharedPeople { get; set; } = new();
    public List<ForbiddenCategoryDto> ForbiddenSongsByCategory { get; set; } = new();
}

public class SharedPersonDto
{
    public int PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string RoleInThatWedding { get; set; } = string.Empty;
}

public class ForbiddenCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public List<ForbiddenSongDto> Songs { get; set; } = new();
}

public class ForbiddenSongDto
{
    public int SongId { get; set; }
    public string SongTitle { get; set; } = string.Empty;
}
