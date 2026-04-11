using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Empleados;

namespace Gv.Rh.Application.Abstractions.Reports;

public interface IEmpleadosReportService
{
    Task<ReportFileDto> BuildXlsxAsync(EmpleadosReporteQueryDto query, CancellationToken cancellationToken);
    Task<ReportFileDto> BuildPdfAsync(EmpleadosReporteQueryDto query, CancellationToken cancellationToken);
}