namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class WeddingDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime DateOfWedding { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Location { get; set; }
    public bool IsFinalized { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public string? Notes { get; set; }
    public int? WeddingSongIntroId { get; set; }
    public string? WeddingSongIntroTitle { get; set; }
    public int? FatherMotherWeddingSongIntroGroomId { get; set; }
    public string? FatherMotherWeddingSongIntroGroomTitle { get; set; }
    public int? FatherMotherWeddingSongIntroBrideId { get; set; }
    public string? FatherMotherWeddingSongIntroBrideTitle { get; set; }
    public List<AvailableSongDto> AvailableWeddingIntroSongs { get; set; } = new();
    public List<AvailableSongDto> AvailableParentIntroSongs { get; set; } = new();
    public List<WeddingRoleDto> Roles { get; set; } = new();
    public ConflictReportDto? ConflictReport { get; set; }
}
