using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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
        mainPart.Document = new Document(
            new Body(
                new Paragraph(
                    new Run(
                        new RunProperties(new Bold()),
                        new Text(title)
                    )
                ),
                new Paragraph(new Run(new Text(string.Empty)))
            )
        );
        mainPart.Document.Save();
        return path;
    }

    public Stream GenerateCombinedDocx(int weddingId, List<(string roleLabel, string songTitle, string filePath)> songs)
    {
        var memStream = new MemoryStream();

        using (var combined = WordprocessingDocument.Create(memStream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = combined.AddMainDocumentPart();
            var body = new Body();
            mainPart.Document = new Document(body);

            bool first = true;
            foreach (var (roleLabel, songTitle, filePath) in songs)
            {
                var absolutePath = Path.Combine(_storageRoot, filePath);
                if (!File.Exists(absolutePath)) continue;

                if (!first)
                {
                    // Page break before each new song
                    body.AppendChild(new Paragraph(
                        new Run(new Break { Type = BreakValues.Page })
                    ));
                }
                first = false;

                // Section header: "Role — Song Title"
                body.AppendChild(new Paragraph(
                    new ParagraphProperties(
                        new ParagraphStyleId { Val = "Heading1" }
                    ),
                    new Run(new Text($"{roleLabel} — {songTitle}"))
                ));

                // Copy paragraphs from source document
                using var sourceDoc = WordprocessingDocument.Open(absolutePath, false);
                var sourceBody = sourceDoc.MainDocumentPart?.Document?.Body;
                if (sourceBody != null)
                {
                    foreach (var element in sourceBody.Elements())
                    {
                        body.AppendChild(element.CloneNode(true));
                    }
                }
            }

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
