using Microsoft.AspNetCore.Mvc;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Controllers;

[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private readonly IDocxService _docx;
    private readonly IExcelExportService _excel;
    private readonly IWeddingService _weddings;
    private readonly IConflictDetectionService _conflicts;
    private readonly IWeddingFolderService _folder;

    public ExportController(IDocxService docx, IExcelExportService excel,
        IWeddingService weddings, IConflictDetectionService conflicts, IWeddingFolderService folder)
    {
        _docx = docx;
        _excel = excel;
        _weddings = weddings;
        _conflicts = conflicts;
        _folder = folder;
    }

    [HttpGet("{weddingId:int}/individual/{roleType}")]
    public async Task<IActionResult> DownloadIndividual(int weddingId, RoleType roleType)
    {
        var data = await _weddings.GetRoleSongExportDataAsync(weddingId, roleType);
        if (data == null) return NotFound("Role not found or no song assigned.");

        var (roleLabel, filePath, songTitle) = data.Value;
        var stream = _docx.GetSongStream(filePath);
        var fileName = $"{roleLabel}_{songTitle}.docx";
        return File(stream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }

    [HttpGet("{weddingId:int}/open/{roleType}")]
    public async Task<IActionResult> OpenIndividualInWord(int weddingId, RoleType roleType)
    {
        // Prefer the copy in the wedding output folder; fall back to the source song library.
        var weddingFolderPath = await _folder.GetRoleSongPathAsync(weddingId, roleType);
        if (weddingFolderPath != null)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = weddingFolderPath,
                UseShellExecute = true
            });
            return Ok();
        }

        var data = await _weddings.GetRoleSongExportDataAsync(weddingId, roleType);
        if (data == null) return NotFound("Role not found or no song assigned.");

        _docx.OpenFileInWord(data.Value.filePath);
        return Ok();
    }

    [HttpGet("{weddingId:int}/combined")]
    public async Task<IActionResult> DownloadCombined(int weddingId)
    {
        var songs = await _weddings.GetCombinedExportDataAsync(weddingId);
        if (!songs.Any()) return BadRequest("No songs assigned.");

        var stream = _docx.GenerateCombinedDocx(weddingId, songs);
        var fileName = $"Wedding_{weddingId}_Combined.docx";
        return File(stream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }

    [HttpGet("{weddingId:int}/excel")]
    public async Task<IActionResult> DownloadExcel(int weddingId)
    {
        var wedding = await _weddings.GetByIdAsync(weddingId);
        var conflicts = await _conflicts.GetConflictReportAsync(weddingId);

        var stream = _excel.GenerateConflictReport(wedding, conflicts);
        var fileName = $"Wedding_{weddingId}_ConflictReport.xlsx";
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
