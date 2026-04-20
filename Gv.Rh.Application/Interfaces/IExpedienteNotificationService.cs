namespace Gv.Rh.Application.Interfaces;

public interface IExpedienteNotificationService
{
    Task<int> NotificarDocumentosPorVencerAsync(
        int diasAnticipacion,
        CancellationToken cancellationToken = default);
}