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
    public DateTime? UpdatedDate { get; set; }

    public int? WeddingSongIntroId { get; set; }
    public int? FatherMotherWeddingSongIntroGroomId { get; set; }
    public int? FatherMotherWeddingSongIntroBrideId { get; set; }

    public Song? WeddingSongIntro { get; set; }
    public Song? FatherMotherWeddingSongIntroGroom { get; set; }
    public Song? FatherMotherWeddingSongIntroBride { get; set; }

    public ICollection<WeddingDetail> Details { get; set; } = new List<WeddingDetail>();
}
