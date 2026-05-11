using ClosedXML.Excel;
using WeddingOrchestrator.Api.DTOs.Weddings;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class ExcelExportService : IExcelExportService
{
    public Stream GenerateConflictReport(WeddingDto wedding, ConflictReportDto conflicts)
    {
        using var workbook = new XLWorkbook();

        // Sheet 1: Songs Used at This Wedding
        var usedSheet = workbook.Worksheets.Add("Songs Used");
        usedSheet.Cell(1, 1).Value = "Role";
        usedSheet.Cell(1, 2).Value = "Song Title";
        usedSheet.Cell(1, 3).Value = "Category";
        usedSheet.Cell(1, 4).Value = "Slot";
        StyleHeader(usedSheet.Row(1));

        int row = 2;
        foreach (var role in wedding.Roles)
        {
            foreach (var a in role.SongAssignments)
            {
                usedSheet.Cell(row, 1).Value = role.RoleLabel;
                usedSheet.Cell(row, 2).Value = a.SongTitle ?? string.Empty;
                usedSheet.Cell(row, 3).Value = a.SongCategoryName ?? string.Empty;
                usedSheet.Cell(row, 4).Value = a.AssignmentSlot == 1 ? "Main" : "Intro";
                row++;
            }
        }
        usedSheet.Columns().AdjustToContents();

        // Sheet 2: Forbidden Songs (from previous weddings)
        var forbiddenSheet = workbook.Worksheets.Add("Forbidden Songs");
        forbiddenSheet.Cell(1, 1).Value = "Previous Wedding";
        forbiddenSheet.Cell(1, 2).Value = "Shared Person";
        forbiddenSheet.Cell(1, 3).Value = "Role in That Wedding";
        forbiddenSheet.Cell(1, 4).Value = "Forbidden Song";
        forbiddenSheet.Cell(1, 5).Value = "Category";
        StyleHeader(forbiddenSheet.Row(1));

        row = 2;
        foreach (var cw in conflicts.ConflictingWeddings)
        {
            foreach (var cat in cw.ForbiddenSongsByCategory)
            {
                foreach (var song in cat.Songs)
                {
                    forbiddenSheet.Cell(row, 1).Value = cw.WeddingTitle;
                    var sharedPeople = string.Join(", ", cw.SharedPeople.Select(p => p.PersonName));
                    var roles = string.Join(", ", cw.SharedPeople.Select(p => p.RoleInThatWedding));
                    forbiddenSheet.Cell(row, 2).Value = sharedPeople;
                    forbiddenSheet.Cell(row, 3).Value = roles;
                    forbiddenSheet.Cell(row, 4).Value = song.SongTitle;
                    forbiddenSheet.Cell(row, 5).Value = cat.CategoryName;
                    row++;
                }
            }
        }
        forbiddenSheet.Columns().AdjustToContents();

        // Sheet 3: Conflict Summary
        var summarySheet = workbook.Worksheets.Add("Conflict Summary");
        summarySheet.Cell(1, 1).Value = "Previous Wedding";
        summarySheet.Cell(1, 2).Value = "Shared People";
        summarySheet.Cell(1, 3).Value = "Total Forbidden Songs";
        StyleHeader(summarySheet.Row(1));

        row = 2;
        foreach (var cw in conflicts.ConflictingWeddings)
        {
            summarySheet.Cell(row, 1).Value = cw.WeddingTitle;
            summarySheet.Cell(row, 2).Value = string.Join(", ", cw.SharedPeople.Select(p => p.PersonName));
            summarySheet.Cell(row, 3).Value = cw.ForbiddenSongsByCategory.Sum(c => c.Songs.Count);
            row++;
        }
        summarySheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void StyleHeader(IXLRow row)
    {
        row.Style.Font.Bold = true;
        row.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        row.Style.Font.FontColor = XLColor.White;
    }
}
