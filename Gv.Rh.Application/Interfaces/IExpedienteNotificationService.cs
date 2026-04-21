using Gv.Rh.Application.DTOs.EmpleadosDocumentos;

namespace Gv.Rh.Application.Interfaces;

public interface IExpedienteNotificationService
{
    Task<DocumentoVencimientoResumenDto> ObtenerResumenAsync(
        int diasAnticipacion,
        bool incluirVencidos,
        CancellationToken cancellationToken = default);

    Task<int> EnviarResumenAsync(
        int diasAnticipacion,
        bool incluirVencidos,
        CancellationToken cancellationToken = default);
}