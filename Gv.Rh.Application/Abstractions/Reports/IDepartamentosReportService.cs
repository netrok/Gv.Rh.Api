using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Departamentos;

namespace Gv.Rh.Application.Abstractions.Reports;

public interface IDepartamentosReportService
{
    Task<ReportFileDto> BuildXlsxAsync(DepartamentoReporteQueryDto query, CancellationToken cancellationToken);
    Task<ReportFileDto> BuildPdfAsync(DepartamentoReporteQueryDto query, CancellationToken cancellationToken);
}