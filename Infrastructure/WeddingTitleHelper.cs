using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Infrastructure;

public static class WeddingTitleHelper
{
    public static string Compute(Wedding wedding)
    {
        var groomRole = wedding.Roles.FirstOrDefault(r => r.RoleType == RoleType.Groom);
        var brideRole = wedding.Roles.FirstOrDefault(r => r.RoleType == RoleType.Bride);

        var groomName = groomRole?.Person?.LastName ?? groomRole?.FreeTextName?.Split(' ').LastOrDefault() ?? "(Groom TBD)";
        var brideName = brideRole?.Person?.LastName ?? brideRole?.FreeTextName?.Split(' ').LastOrDefault() ?? "(Bride TBD)";

        return $"{groomName} - {brideName} - {wedding.DateOfWedding:MMM dd, yyyy}";
    }
}
