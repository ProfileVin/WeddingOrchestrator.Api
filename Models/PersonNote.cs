namespace WeddingOrchestrator.Api.Models;

public class PersonNote
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public int? WeddingId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Person Person { get; set; } = null!;
    public Wedding? Wedding { get; set; }
}
