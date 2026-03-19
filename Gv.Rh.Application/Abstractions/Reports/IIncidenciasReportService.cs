using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Incidencias;

namespace Gv.Rh.Application.Abstractions.Reports;

public interface IIncidenciasReportService
{
    Task<ReportFileDto> BuildXlsxAsync(IncidenciaQueryDto query, CancellationToken cancellationToken);
    Task<ReportFileDto> BuildPdfAsync(IncidenciaQueryDto query, CancellationToken cancellationToken);
}