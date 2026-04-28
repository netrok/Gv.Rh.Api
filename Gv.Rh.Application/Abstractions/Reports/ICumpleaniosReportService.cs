using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Cumpleanios;

namespace Gv.Rh.Application.Abstractions.Reports;

public interface ICumpleaniosReportService
{
    Task<ReportFileDto> BuildXlsxAsync(
        CumpleaniosReporteQueryDto query,
        CancellationToken cancellationToken);

    Task<ReportFileDto> BuildPdfAsync(
        CumpleaniosReporteQueryDto query,
        CancellationToken cancellationToken);
}