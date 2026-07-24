using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Services;

public class DocxService : IDocxService
{
    private readonly string _storageRoot;

    public DocxService(IConfiguration config)
    {
        _storageRoot = config["SongStoragePath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "wedding-orchestrator", "songs");
    }

    public string CreateBlankDocx(string title, string directory)
    {
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}_{Sanitize(title)}.docx";
        var path = Path.Combine(directory, fileName);

        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        var body = new Body();
        body.AppendChild(new Paragraph());
        var document = new Document();
        document.AppendChild(body);
        mainPart.Document = document;
        mainPart.Document.Save();
        return path;
    }

    // Each row in RoleHelper.MasterPerformancePairs is a married couple, rendered as a
    // borderless 2-column table: husband's song in the left cell, wife's song in the right
    // cell. A table (rather than a real multi-column section + column break) sidesteps a
    // Word quirk where a column-break-only paragraph still reserves a blank line at the top
    // of the new column — each cell here just lays out its own paragraphs independently, with
    // no break character involved. A page break separates one couple's row from the next.
    // Intro tracks aren't a family member's personal song, so they render full-width, single
    // column, after all the couple rows.
    public Stream GenerateCombinedDocx(int weddingId, List<(string roleLabel, string personName, string songTitle, string filePath, RoleType? roleType)> songs)
    {
        var memStream = new MemoryStream();

        using (var combined = WordprocessingDocument.Create(memStream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = combined.AddMainDocumentPart();
            var body = new Body();
            mainPart.Document = new Document(body);

            var byRole = songs.Where(s => s.roleType.HasValue).ToDictionary(s => s.roleType!.Value, s => s);
            var sharedEntries = songs.Where(s => !s.roleType.HasValue).ToList();

            // Writes a heading + the song's cloned paragraphs into any container (a table
            // cell or the document body). Skips a source document's own bare SectionProperties
            // (invalid inside a table cell, and redundant once cloned into the combined doc).
            void AppendSongContent(OpenXmlCompositeElement container, (string roleLabel, string personName, string songTitle, string filePath, RoleType? roleType) entry)
            {
                var headerText = string.IsNullOrWhiteSpace(entry.personName)
                    ? entry.roleLabel
                    : $"{entry.roleLabel} - {entry.personName}";

                container.AppendChild(new Paragraph(
                    new ParagraphProperties(
                        new ParagraphStyleId { Val = "Heading1" }
                    ),
                    new Run(new Text(headerText))
                ));

                var absolutePath = Path.Combine(_storageRoot, entry.filePath);
                using var sourceDoc = WordprocessingDocument.Open(absolutePath, false);
                var sourceBody = sourceDoc.MainDocumentPart?.Document?.Body;
                if (sourceBody != null)
                {
                    foreach (var element in sourceBody.Elements())
                    {
                        if (element is SectionProperties) continue;
                        container.AppendChild(element.CloneNode(true));
                    }
                }
            }

            var anyRowRendered = false;

            foreach (var (maleRole, femaleRole) in RoleHelper.MasterPerformancePairs)
            {
                var hasMale = byRole.TryGetValue(maleRole, out var maleEntry);
                var hasFemale = byRole.TryGetValue(femaleRole, out var femaleEntry);
                var maleFileExists = hasMale && File.Exists(Path.Combine(_storageRoot, maleEntry.filePath));
                var femaleFileExists = hasFemale && File.Exists(Path.Combine(_storageRoot, femaleEntry.filePath));
                if (!maleFileExists && !femaleFileExists) continue;

                if (anyRowRendered)
                {
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }

                var table = new Table(
                    new TableProperties(
                        new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                        new TableBorders(
                            new TopBorder { Val = BorderValues.None },
                            new BottomBorder { Val = BorderValues.None },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None },
                            new InsideHorizontalBorder { Val = BorderValues.None },
                            new InsideVerticalBorder { Val = BorderValues.None }
                        )
                    ),
                    new TableGrid(new GridColumn(), new GridColumn())
                );

                var leftCell = new TableCell(new TableCellProperties(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Pct }));
                var rightCell = new TableCell(new TableCellProperties(new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Pct }));

                if (maleFileExists) AppendSongContent(leftCell, maleEntry); else leftCell.AppendChild(new Paragraph());
                if (femaleFileExists) AppendSongContent(rightCell, femaleEntry); else rightCell.AppendChild(new Paragraph());

                table.AppendChild(new TableRow(leftCell, rightCell));
                body.AppendChild(table);

                anyRowRendered = true;
            }

            if (anyRowRendered && sharedEntries.Count > 0)
            {
                body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            }

            var sharedFirst = true;
            foreach (var entry in sharedEntries)
            {
                var absolutePath = Path.Combine(_storageRoot, entry.filePath);
                if (!File.Exists(absolutePath)) continue;

                if (!sharedFirst)
                {
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }
                AppendSongContent(body, entry);
                sharedFirst = false;
            }

            body.AppendChild(new SectionProperties());

            mainPart.Document.Save();
        }

        memStream.Position = 0;
        return memStream;
    }

    public Stream GetSongStream(string relativePath)
    {
        var absolutePath = Path.Combine(_storageRoot, relativePath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Song file not found.");
        return new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void OpenFileInWord(string relativePath)
    {
        var absolutePath = Path.Combine(_storageRoot, relativePath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Song file not found.");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = absolutePath,
            UseShellExecute = true
        });
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
}
