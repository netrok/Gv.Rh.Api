using Gv.Rh.Application.DTOs.Reclutamiento.Reportes;

namespace Gv.Rh.Application.Interfaces.Reclutamiento;

public interface IReclutamientoReporteService
{
    Task<byte[]> ExportarXlsxAsync(
        ReclutamientoReporteFiltroDto filtro,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportarPdfAsync(
        ReclutamientoReporteFiltroDto filtro,
        CancellationToken cancellationToken = default);
}