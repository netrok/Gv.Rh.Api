using System.Security.Claims;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Services;

public sealed class EmpleadoAccessScopeService : IEmpleadoAccessScopeService
{
    private readonly RhDbContext _db;

    public EmpleadoAccessScopeService(RhDbContext db)
    {
        _db = db;
    }

    public bool IsAdminOrRrhh(ClaimsPrincipal principal)
    {
        var roles = GetRoles(principal);

        return roles.Contains(UserRoles.Admin)
            || roles.Contains(UserRoles.Rrhh);
    }

    public bool IsJefe(ClaimsPrincipal principal)
    {
        var roles = GetRoles(principal);

        return roles.Contains(UserRoles.Jefe);
    }

    public bool IsEmpleadoOnly(ClaimsPrincipal principal)
    {
        var roles = GetRoles(principal);

        return roles.Contains(UserRoles.Empleado)
            && !roles.Contains(UserRoles.Admin)
            && !roles.Contains(UserRoles.Rrhh)
            && !roles.Contains(UserRoles.Jefe);
    }

    public int? GetCurrentUserId(ClaimsPrincipal principal)
    {
        return TryGetIntClaimValue(
            principal,
            "sub",
            ClaimTypes.NameIdentifier,
            ClaimTypes.Sid,
            "sub",
            "nameid",
            "uid",
            "userId",
            "UserId",
            "id");
    }

    public int? GetCurrentEmpleadoIdFromClaims(ClaimsPrincipal principal)
    {
        return TryGetIntClaimValue(
            principal,
            "empleado_id",
            "empleadoId",
            "EmpleadoId",
            "rh_empleado_id",
            "employeeId",
            "EmployeeId");
    }

    public async Task<int?> ResolveCurrentEmpleadoIdAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var empleadoIdFromClaim = GetCurrentEmpleadoIdFromClaims(principal);

        if (empleadoIdFromClaim.HasValue && empleadoIdFromClaim.Value > 0)
        {
            return empleadoIdFromClaim.Value;
        }

        var userId = GetCurrentUserId(principal);

        if (userId.HasValue && userId.Value > 0)
        {
            var empleadoIdByUserId = await _db.Users
                .AsNoTracking()
                .Where(x => x.Id == userId.Value && x.IsActive)
                .Select(x => x.EmpleadoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdByUserId.HasValue && empleadoIdByUserId.Value > 0)
            {
                return empleadoIdByUserId.Value;
            }
        }

        var email = GetCurrentUserEmail(principal);

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var empleadoIdByUserEmail = await _db.Users
                .AsNoTracking()
                .Where(x => x.IsActive && x.Email.ToLower() == normalizedEmail)
                .Select(x => x.EmpleadoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdByUserEmail.HasValue && empleadoIdByUserEmail.Value > 0)
            {
                return empleadoIdByUserEmail.Value;
            }

            var empleadoIdDirectByEmail = await _db.Empleados
                .AsNoTracking()
                .Where(x => x.Email != null && x.Email.ToLower() == normalizedEmail)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdDirectByEmail.HasValue && empleadoIdDirectByEmail.Value > 0)
            {
                return empleadoIdDirectByEmail.Value;
            }
        }

        return null;
    }

    public IQueryable<Empleado> ApplyEmpleadoScope(
        IQueryable<Empleado> query,
        ClaimsPrincipal principal)
    {
        if (IsAdminOrRrhh(principal))
        {
            return query;
        }

        var empleadoId = GetCurrentEmpleadoIdFromClaims(principal);

        if (!empleadoId.HasValue || empleadoId.Value <= 0)
        {
            return query.Where(x => false);
        }

        if (IsJefe(principal))
        {
            return query.Where(x =>
                x.Id == empleadoId.Value ||
                x.AprobadorPrimarioEmpleadoId == empleadoId.Value ||
                x.AprobadorSecundarioEmpleadoId == empleadoId.Value);
        }

        return query.Where(x => x.Id == empleadoId.Value);
    }

    public IQueryable<Incidencia> ApplyIncidenciaScope(
        IQueryable<Incidencia> query,
        ClaimsPrincipal principal)
    {
        if (IsAdminOrRrhh(principal))
        {
            return query;
        }

        var empleadoId = GetCurrentEmpleadoIdFromClaims(principal);

        if (!empleadoId.HasValue || empleadoId.Value <= 0)
        {
            return query.Where(x => false);
        }

        if (IsJefe(principal))
        {
            return query.Where(x =>
                x.EmpleadoId == empleadoId.Value ||
                x.Empleado.AprobadorPrimarioEmpleadoId == empleadoId.Value ||
                x.Empleado.AprobadorSecundarioEmpleadoId == empleadoId.Value ||
                x.ResueltaPorEmpleadoId == empleadoId.Value);
        }

        return query.Where(x => x.EmpleadoId == empleadoId.Value);
    }

    public IQueryable<VacacionSolicitud> ApplyVacacionSolicitudScope(
        IQueryable<VacacionSolicitud> query,
        ClaimsPrincipal principal)
    {
        if (IsAdminOrRrhh(principal))
        {
            return query;
        }

        var empleadoId = GetCurrentEmpleadoIdFromClaims(principal);

        if (!empleadoId.HasValue || empleadoId.Value <= 0)
        {
            return query.Where(x => false);
        }

        if (IsJefe(principal))
        {
            return query.Where(x =>
                x.EmpleadoId == empleadoId.Value ||
                x.AprobadorEmpleadoId == empleadoId.Value ||
                x.Empleado!.AprobadorPrimarioEmpleadoId == empleadoId.Value ||
                x.Empleado!.AprobadorSecundarioEmpleadoId == empleadoId.Value);
        }

        return query.Where(x => x.EmpleadoId == empleadoId.Value);
    }

    public async Task<bool> CanViewEmpleadoAsync(
        ClaimsPrincipal principal,
        int empleadoId,
        CancellationToken cancellationToken = default)
    {
        if (empleadoId <= 0)
        {
            return false;
        }

        if (IsAdminOrRrhh(principal))
        {
            return true;
        }

        var currentEmpleadoId = await ResolveCurrentEmpleadoIdAsync(principal, cancellationToken);

        if (!currentEmpleadoId.HasValue || currentEmpleadoId.Value <= 0)
        {
            return false;
        }

        if (IsJefe(principal))
        {
            return await _db.Empleados
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == empleadoId &&
                    (
                        x.Id == currentEmpleadoId.Value ||
                        x.AprobadorPrimarioEmpleadoId == currentEmpleadoId.Value ||
                        x.AprobadorSecundarioEmpleadoId == currentEmpleadoId.Value
                    ),
                    cancellationToken);
        }

        return empleadoId == currentEmpleadoId.Value;
    }
    private static HashSet<string> GetRoles(ClaimsPrincipal principal)
    {
        return principal.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role ||
                claim.Type == "role" ||
                claim.Type == "roles" ||
                claim.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(UserRoles.Normalize)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int? TryGetIntClaimValue(
        ClaimsPrincipal principal,
        params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var raw = principal.FindFirstValue(claimType);

            if (int.TryParse(raw, out var value) && value > 0)
            {
                return value;
            }
        }

        return null;
    }

    private static string? GetCurrentUserEmail(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("email")
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");
    }
}