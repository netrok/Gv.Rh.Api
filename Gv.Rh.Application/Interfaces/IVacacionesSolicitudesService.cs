using Gv.Rh.Application.DTOs.Vacaciones.Solicitudes;

namespace Gv.Rh.Application.Interfaces;

public interface IVacacionesSolicitudesService
{
    Task<VacacionesSolicitudListResultDto> GetSolicitudesAsync(
        VacacionesSolicitudQueryDto query,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default);

    Task<VacacionesSolicitudDto> GetSolicitudByIdAsync(
        int id,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default);

    Task<VacacionesSolicitudDto> CreateSolicitudAsync(
        VacacionesSolicitudCreateDto request,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default);

    Task<VacacionesSolicitudDto> AprobarSolicitudAsync(
        int id,
        VacacionesSolicitudResolverDto request,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default);

    Task<VacacionesSolicitudDto> RechazarSolicitudAsync(
        int id,
        VacacionesSolicitudResolverDto request,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default);

    Task<VacacionesSolicitudDto> CancelarSolicitudAsync(
        int id,
        VacacionesSolicitudResolverDto request,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default);
}