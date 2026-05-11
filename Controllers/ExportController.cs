using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Data;
using WeddingOrchestrator.Api.Infrastructure;
using WeddingOrchestrator.Api.Models.Enums;
using WeddingOrchestrator.Api.Services.Interfaces;

namespace WeddingOrchestrator.Api.Controllers;

[ApiController]
[Route("api/export")]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IDocxService _docx;
    private readonly IExcelExportService _excel;
    private readonly IWeddingService _weddings;
    private readonly IConflictDetectionService _conflicts;

    public ExportController(AppDbContext db, IDocxService docx, IExcelExportService excel,
        IWeddingService weddings, IConflictDetectionService conflicts)
    {
        _db = db;
        _docx = docx;
        _excel = excel;
        _weddings = weddings;
        _conflicts = conflicts;
    }

    [HttpGet("{weddingId:int}/individual/{roleType}")]
    public async Task<IActionResult> DownloadIndividual(int weddingId, RoleType roleType)
    {
        var role = await _db.WeddingRoles
            .Include(r => r.SongAssignments).ThenInclude(a => a.Song)
            .FirstOrDefaultAsync(r => r.WeddingId == weddingId && r.RoleType == roleType);

        if (role == null) return NotFound("Role not found.");

        var assignment = role.SongAssignments.FirstOrDefault(a => a.AssignmentSlot == 1);
        if (assignment == null) return NotFound("No song assigned.");

        var stream = _docx.GetSongStream(assignment.Song.RelativeFilePath);
        var fileName = $"{RoleHelper.GetLabel(roleType)}_{assignment.Song.Title}.docx";
        return File(stream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }

    [HttpGet("{weddingId:int}/combined")]
    public async Task<IActionResult> DownloadCombined(int weddingId)
    {
        var wedding = await _db.Weddings
            .Include(w => w.Roles).ThenInclude(r => r.SongAssignments).ThenInclude(a => a.Song)
            .FirstOrDefaultAsync(w => w.Id == weddingId);

        if (wedding == null) return NotFound();

        var songs = wedding.Roles
            .OrderBy(r => r.RoleType)
            .SelectMany(r => r.SongAssignments.OrderBy(a => a.AssignmentSlot)
                .Select(a => (RoleHelper.GetLabel(r.RoleType), a.Song.Title, a.Song.RelativeFilePath)))
            .ToList();

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
