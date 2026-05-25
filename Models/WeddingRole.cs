using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Models;

public class WeddingRole
{
    public int Id { get; set; }
    public int WeddingId { get; set; }
    public RoleType RoleType { get; set; }

    public int? PersonId { get; set; }

    public Wedding Wedding { get; set; } = null!;
    public Person? Person { get; set; }
    public ICollection<WeddingRoleSongAssignment> SongAssignments { get; set; } = new List<WeddingRoleSongAssignment>();
}
