using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Vacaciones.Reports;

namespace Gv.Rh.Application.Abstractions.Reports;

public interface IVacacionesReportService
{
    Task<VacacionesSaldosReporteResultDto> GetSaldosAsync(
        VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken);

    Task<ReportFileDto> BuildSaldosXlsxAsync(
        VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken);

    Task<ReportFileDto> BuildSaldosPdfAsync(
        VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken);

    Task<VacacionesKardexReporteResultDto> GetKardexAsync(
        VacacionesKardexReporteQueryDto query,
        CancellationToken cancellationToken);

    Task<ReportFileDto> BuildKardexXlsxAsync(
        VacacionesKardexReporteQueryDto query,
        CancellationToken cancellationToken);

    Task<ReportFileDto> BuildKardexPdfAsync(
        VacacionesKardexReporteQueryDto query,
        CancellationToken cancellationToken);
}