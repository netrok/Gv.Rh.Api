namespace Gv.Rh.Application.Interfaces;

public interface IAprobacionesNotificationService
{
    Task<int> NotificarResumenPendientesAsync(
        CancellationToken cancellationToken = default);
}
