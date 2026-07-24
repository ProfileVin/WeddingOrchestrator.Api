using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IDocxService
{
    string CreateBlankDocx(string title, string directory);
    Stream GenerateCombinedDocx(int weddingId, List<(string roleLabel, string personName, string songTitle, string filePath, RoleType? roleType)> songs);
    Stream GetSongStream(string relativePath);
    void OpenFileInWord(string relativePath);
}
