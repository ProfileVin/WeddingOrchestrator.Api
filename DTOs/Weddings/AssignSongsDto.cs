namespace WeddingOrchestrator.Api.DTOs.Weddings;

public class AssignSongsDto
{
    public List<SongAssignmentInputDto> Assignments { get; set; } = new();
}

public class SongAssignmentInputDto
{
    public int WeddingRoleId { get; set; }
    public int? PersonId { get; set; }
    public int AssignmentSlot { get; set; }
    public int SongId { get; set; }
}
