using WeddingOrchestrator.Api.DTOs.Weddings;

namespace WeddingOrchestrator.Api.Services.Interfaces;

public interface IExcelExportService
{
    Stream GenerateConflictReport(WeddingDto wedding, ConflictReportDto conflicts);
}
