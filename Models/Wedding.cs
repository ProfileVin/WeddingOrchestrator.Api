namespace WeddingOrchestrator.Api.Models;

public class Wedding
{
    public int Id { get; set; }
    public DateTime DateOfWedding { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Location { get; set; }
    public bool IsFinalized { get; set; }
    public DateTime CreatedUtc { get; set; }

    public ICollection<WeddingRole> Roles { get; set; } = new List<WeddingRole>();
}
