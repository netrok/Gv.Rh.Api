using Gv.Rh.Application.DTOs.Vacaciones;

namespace Gv.Rh.Application.Interfaces;

public interface IVacacionesService
{
    Task<VacacionesResumenDto> GetResumenAsync(int empleadoId, CancellationToken cancellationToken = default);
    Task<List<VacacionPeriodoDto>> GetPeriodosAsync(int empleadoId, CancellationToken cancellationToken = default);
    Task<List<VacacionMovimientoDto>> GetKardexAsync(int empleadoId, CancellationToken cancellationToken = default);
    Task<VacacionPeriodoDto> GenerarPeriodoAsync(int empleadoId, VacacionGenerarPeriodoRequestDto request, int? usuarioResponsableId, CancellationToken cancellationToken = default);
    Task<VacacionMovimientoDto> RegistrarDisfruteAsync(int empleadoId, VacacionRegistrarDisfruteRequestDto request, int? usuarioResponsableId, CancellationToken cancellationToken = default);
    Task<VacacionMovimientoDto> RegistrarAjusteAsync(int empleadoId, VacacionAjusteRequestDto request, int? usuarioResponsableId, CancellationToken cancellationToken = default);
}
