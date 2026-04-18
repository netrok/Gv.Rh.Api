using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Sucursales;

namespace Gv.Rh.Application.Abstractions.Reports;

public interface ISucursalesReportService
{
    Task<ReportFileDto> BuildXlsxAsync(SucursalReporteQueryDto query, CancellationToken cancellationToken);
    Task<ReportFileDto> BuildPdfAsync(SucursalReporteQueryDto query, CancellationToken cancellationToken);
}