using System.Security.Claims;

namespace Gv.Rh.Application.Interfaces;

public interface IIncidenciaAuthorizationService
{
    bool IsAdminOrRrhh(ClaimsPrincipal principal);
    int? GetCurrentUserId(ClaimsPrincipal principal);
    int? GetCurrentEmpleadoId(ClaimsPrincipal principal);

    Task<bool> CanViewAsync(
        ClaimsPrincipal principal,
        int incidenciaId,
        CancellationToken cancellationToken = default);

    Task<bool> CanResolveAsync(
        ClaimsPrincipal principal,
        int incidenciaId,
        CancellationToken cancellationToken = default);
}