using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class WeddingRoleDto
{
    public int Id { get; set; }
    public RoleType RoleType { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public int? PersonId { get; set; }
    public string? PersonName { get; set; }
    public string? PersonFirstName { get; set; }
    public string? PersonLastName { get; set; }
    public string DisplayName => PersonName ?? string.Empty;
    public List<SongAssignmentDto> SongAssignments { get; set; } = new();
    public List<AvailableSongDto> AvailableSongs { get; set; } = new();
}

public class SongAssignmentDto
{
    public int AssignmentSlot { get; set; }
    public int? SongId { get; set; }
    public string? SongTitle { get; set; }
    public string? SongCategoryName { get; set; }
    public long FileSizeBytes { get; set; }
}

public class AvailableSongDto
{
    public int SongId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int AssignmentSlot { get; set; }
    public bool IsForbidden { get; set; }
    public bool IsPrimaryCategory { get; set; }
}
