using Gv.Rh.Application.DTOs.Common;

namespace Gv.Rh.Application.Abstractions.Reports;

public interface IEmpleadoFichaReportService
{
    Task<ReportFileDto> BuildPdfAsync(int empleadoId, CancellationToken cancellationToken);
}