namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IDocxService
{
    string CreateBlankDocx(string title, string directory);
    Stream GenerateCombinedDocx(int weddingId, List<(string roleLabel, string songTitle, string filePath)> songs);
    Stream GetSongStream(string relativePath);
}
