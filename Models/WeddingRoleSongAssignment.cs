namespace WeddingOrchestrator.Api.Models;

public class WeddingRoleSongAssignment
{
    public int Id { get; set; }
    public int WeddingRoleId { get; set; }
    public int SongId { get; set; }
    public int AssignmentSlot { get; set; } // 1 = primary/main, 2 = intro

    public WeddingRole WeddingRole { get; set; } = null!;
    public Song Song { get; set; } = null!;
}
