namespace Gv.Rh.Application.Interfaces;

public interface IVacacionesSolicitudesNotificationService
{
    Task NotificarSolicitudCreadaAsync(
        int solicitudId,
        CancellationToken cancellationToken = default);

    Task NotificarSolicitudResueltaAsync(
        int solicitudId,
        string estatus,
        CancellationToken cancellationToken = default);
}