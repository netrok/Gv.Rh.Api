using System.Security.Claims;
using Gv.Rh.Domain.Entities;

namespace Gv.Rh.Application.Interfaces;

public interface IEmpleadoAccessScopeService
{
    bool IsAdminOrRrhh(ClaimsPrincipal principal);

    bool IsJefe(ClaimsPrincipal principal);

    bool IsEmpleadoOnly(ClaimsPrincipal principal);

    int? GetCurrentUserId(ClaimsPrincipal principal);

    int? GetCurrentEmpleadoIdFromClaims(ClaimsPrincipal principal);

    Task<int?> ResolveCurrentEmpleadoIdAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    IQueryable<Empleado> ApplyEmpleadoScope(
        IQueryable<Empleado> query,
        ClaimsPrincipal principal);

    IQueryable<Incidencia> ApplyIncidenciaScope(
        IQueryable<Incidencia> query,
        ClaimsPrincipal principal);

    IQueryable<VacacionSolicitud> ApplyVacacionSolicitudScope(
        IQueryable<VacacionSolicitud> query,
        ClaimsPrincipal principal);

    Task<bool> CanViewEmpleadoAsync(
        ClaimsPrincipal principal,
        int empleadoId,
        CancellationToken cancellationToken = default);
}