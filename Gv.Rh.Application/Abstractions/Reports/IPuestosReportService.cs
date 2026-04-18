using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Puestos;

namespace Gv.Rh.Application.Abstractions.Reports;

public interface IPuestosReportService
{
    Task<ReportFileDto> BuildXlsxAsync(PuestoReporteQueryDto query, CancellationToken cancellationToken);
    Task<ReportFileDto> BuildPdfAsync(PuestoReporteQueryDto query, CancellationToken cancellationToken);
}