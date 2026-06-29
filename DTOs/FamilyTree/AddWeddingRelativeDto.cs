using System.ComponentModel.DataAnnotations;

namespace WeddingOrchestrator.Api.DTOs.FamilyTree;

public class AddWeddingRelativeDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Relationship of the new person TO the related person (e.g. BROTHER, SISTER).</summary>
    [Required]
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>PersonId of the Groom or Bride this person is related to.</summary>
    public int RelatedPersonId { get; set; }

    /// <summary>Wedding to save this person into WeddingDetails. Required to create a WeddingDetail row.</summary>
    public int WeddingId { get; set; }

    // Optional context — the related person's family members.
    // Blood-sibling links (BROTHER / SISTER) use these to create parent/grandparent chains.
    public int? FatherId { get; set; }
    public int? MotherId { get; set; }
    public int? PaternalGrandfatherId { get; set; }
    public int? PaternalGrandmotherId { get; set; }
    public int? MaternalGrandfatherId { get; set; }
    public int? MaternalGrandmotherId { get; set; }

    /// <summary>PersonId of the Groom/Bride's spouse for in-law relationship creation.</summary>
    public int? SpouseId { get; set; }

    /// <summary>True if the spouse is male (Groom). Used to pick BROTHER_IN_LAW vs SISTER_IN_LAW for the inverse.</summary>
    public bool? SpouseIsMale { get; set; }
}
