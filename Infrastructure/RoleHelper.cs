using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Infrastructure;

public static class RoleHelper
{
    public static string GetLabel(RoleType role) => role switch
    {
        RoleType.Groom => "Groom",
        RoleType.Bride => "Bride",
        RoleType.FatherOfGroom => "Father of the Groom",
        RoleType.MotherOfGroom => "Mother of the Groom",
        RoleType.PaternalGrandfatherOfGroom => "Grandfather (Father's side, Groom)",
        RoleType.PaternalGrandmotherOfGroom => "Grandmother (Father's side, Groom)",
        RoleType.MaternalGrandfatherOfGroom => "Grandfather (Mother's side, Groom)",
        RoleType.MaternalGrandmotherOfGroom => "Grandmother (Mother's side, Groom)",
        RoleType.FatherOfBride => "Father of the Bride",
        RoleType.MotherOfBride => "Mother of the Bride",
        RoleType.PaternalGrandfatherOfBride => "Grandfather (Father's side, Bride)",
        RoleType.PaternalGrandmotherOfBride => "Grandmother (Father's side, Bride)",
        RoleType.MaternalGrandfatherOfBride => "Grandfather (Mother's side, Bride)",
        RoleType.MaternalGrandmotherOfBride => "Grandmother (Mother's side, Bride)",
        RoleType.WeddingItself => "Wedding Intro",
        _ => role.ToString()
    };

    // Master Performance combined doc: each row is a married couple, husband (left column)
    // paired with wife (right column) — the groom & bride themselves, then each side's
    // parents, then each side's grandparents, in this display order.
    public static readonly (RoleType male, RoleType female)[] MasterPerformancePairs =
    {
        (RoleType.Groom, RoleType.Bride),
        (RoleType.FatherOfGroom, RoleType.MotherOfGroom),
        (RoleType.FatherOfBride, RoleType.MotherOfBride),
        (RoleType.PaternalGrandfatherOfGroom, RoleType.PaternalGrandmotherOfGroom),
        (RoleType.PaternalGrandfatherOfBride, RoleType.PaternalGrandmotherOfBride),
        (RoleType.MaternalGrandfatherOfGroom, RoleType.MaternalGrandmotherOfGroom),
        (RoleType.MaternalGrandfatherOfBride, RoleType.MaternalGrandmotherOfBride),
    };

    // Returns the song category IDs needed for each slot.
    // slot 1 = primary/main, slot 2 = intro
    public static List<(int slot, int categoryId)> GetRequiredSlots(RoleType role) => role switch
    {
        RoleType.Groom => new() { (1, 1) },
        RoleType.Bride => new() { (1, 2) },
        RoleType.FatherOfGroom or RoleType.FatherOfBride => new() { (1, 3), (2, 7) },
        RoleType.MotherOfGroom or RoleType.MotherOfBride => new() { (1, 4), (2, 7) },
        RoleType.PaternalGrandfatherOfGroom or RoleType.MaternalGrandfatherOfGroom
            or RoleType.PaternalGrandfatherOfBride or RoleType.MaternalGrandfatherOfBride => new() { (1, 6) },
        RoleType.PaternalGrandmotherOfGroom or RoleType.MaternalGrandmotherOfGroom
            or RoleType.PaternalGrandmotherOfBride or RoleType.MaternalGrandmotherOfBride => new() { (1, 5) },
        RoleType.WeddingItself => new() { (1, 8) },
        _ => new()
    };

    public static IEnumerable<RoleType> AllRoles =>
        Enum.GetValues<RoleType>();
}
