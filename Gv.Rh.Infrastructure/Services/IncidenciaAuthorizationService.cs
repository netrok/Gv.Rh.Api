using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gv.Rh.Infrastructure.Services;

public sealed class IncidenciaAuthorizationService : IIncidenciaAuthorizationService
{
    private const string EmpleadoIdClaimType = "empleado_id";

    private readonly RhDbContext _context;

    public IncidenciaAuthorizationService(RhDbContext context)
    {
        _context = context;
    }

    public bool IsAdminOrRrhh(ClaimsPrincipal principal)
    {
        var role = GetRole(principal);
        return UserRoles.IsAdmin(role) || UserRoles.IsRrhh(role);
    }

    public int? GetCurrentUserId(ClaimsPrincipal principal)
    {
        var value =
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue("sub");

        return int.TryParse(value, out var userId) ? userId : null;
    }

    public int? GetCurrentEmpleadoId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(EmpleadoIdClaimType);
        return int.TryParse(value, out var empleadoId) ? empleadoId : null;
    }

    public async Task<bool> CanViewAsync(
        ClaimsPrincipal principal,
        int incidenciaId,
        CancellationToken cancellationToken = default)
    {
        if (IsAdminOrRrhh(principal))
        {
            return true;
        }

        var empleadoId = GetCurrentEmpleadoId(principal);
        if (!empleadoId.HasValue)
        {
            return false;
        }

        var data = await _context.Incidencias
            .AsNoTracking()
            .Where(x => x.Id == incidenciaId)
            .Select(x => new
            {
                x.EmpleadoId,
                x.Empleado.AprobadorPrimarioEmpleadoId,
                x.Empleado.AprobadorSecundarioEmpleadoId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null)
        {
            return false;
        }

        return data.EmpleadoId == empleadoId.Value
            || data.AprobadorPrimarioEmpleadoId == empleadoId.Value
            || data.AprobadorSecundarioEmpleadoId == empleadoId.Value;
    }

    public async Task<bool> CanResolveAsync(
        ClaimsPrincipal principal,
        int incidenciaId,
        CancellationToken cancellationToken = default)
    {
        if (IsAdminOrRrhh(principal))
        {
            return true;
        }

        var empleadoId = GetCurrentEmpleadoId(principal);
        if (!empleadoId.HasValue)
        {
            return false;
        }

        var data = await _context.Incidencias
            .AsNoTracking()
            .Where(x => x.Id == incidenciaId)
            .Select(x => new
            {
                x.EmpleadoId,
                x.Empleado.AprobadorPrimarioEmpleadoId,
                x.Empleado.AprobadorSecundarioEmpleadoId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null)
        {
            return false;
        }

        if (data.EmpleadoId == empleadoId.Value)
        {
            return false;
        }

        return data.AprobadorPrimarioEmpleadoId == empleadoId.Value
            || data.AprobadorSecundarioEmpleadoId == empleadoId.Value;
    }

    private static string? GetRole(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Role)
            ?? principal.FindFirstValue("role");
    }
}