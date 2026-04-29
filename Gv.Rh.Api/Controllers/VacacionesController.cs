using Gv.Rh.Application.DTOs.Vacaciones;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class VacacionesController : ControllerBase
{
    private readonly IVacacionesService _vacacionesService;
    private readonly RhDbContext _db;

    public VacacionesController(
        IVacacionesService vacacionesService,
        RhDbContext db)
    {
        _vacacionesService = vacacionesService;
        _db = db;
    }

    [HttpGet("Vacaciones/empleado/{empleadoId:int}/resumen")]
    public async Task<IActionResult> GetResumenEmpleado(
        int empleadoId,
        CancellationToken cancellationToken)
    {
        var accessDenied = await EnsureCanAccessEmpleadoAsync(empleadoId, cancellationToken);
        if (accessDenied is not null)
            return accessDenied;

        try
        {
            return Ok(await _vacacionesService.GetResumenAsync(empleadoId, cancellationToken));
        }
        catch (Exception ex)
        {
            return ToActionResult(ex);
        }
    }

    [HttpGet("Vacaciones/empleado/{empleadoId:int}/periodos")]
    public async Task<IActionResult> GetPeriodosEmpleado(
        int empleadoId,
        CancellationToken cancellationToken)
    {
        var accessDenied = await EnsureCanAccessEmpleadoAsync(empleadoId, cancellationToken);
        if (accessDenied is not null)
            return accessDenied;

        try
        {
            return Ok(await _vacacionesService.GetPeriodosAsync(empleadoId, cancellationToken));
        }
        catch (Exception ex)
        {
            return ToActionResult(ex);
        }
    }

    [HttpGet("Vacaciones/empleado/{empleadoId:int}/kardex")]
    public async Task<IActionResult> GetKardexEmpleado(
        int empleadoId,
        CancellationToken cancellationToken)
    {
        var accessDenied = await EnsureCanAccessEmpleadoAsync(empleadoId, cancellationToken);
        if (accessDenied is not null)
            return accessDenied;

        try
        {
            return Ok(await _vacacionesService.GetKardexAsync(empleadoId, cancellationToken));
        }
        catch (Exception ex)
        {
            return ToActionResult(ex);
        }
    }

    [HttpGet("me/vacaciones/resumen")]
    public async Task<IActionResult> GetMiResumen(CancellationToken cancellationToken)
    {
        var empleadoId = await ResolveCurrentEmpleadoIdAsync(cancellationToken);
        if (!empleadoId.HasValue)
            return Forbid();

        try
        {
            return Ok(await _vacacionesService.GetResumenAsync(empleadoId.Value, cancellationToken));
        }
        catch (Exception ex)
        {
            return ToActionResult(ex);
        }
    }

    [HttpGet("me/vacaciones/periodos")]
    public async Task<IActionResult> GetMisPeriodos(CancellationToken cancellationToken)
    {
        var empleadoId = await ResolveCurrentEmpleadoIdAsync(cancellationToken);
        if (!empleadoId.HasValue)
            return Forbid();

        try
        {
            return Ok(await _vacacionesService.GetPeriodosAsync(empleadoId.Value, cancellationToken));
        }
        catch (Exception ex)
        {
            return ToActionResult(ex);
        }
    }

    [HttpGet("me/vacaciones/kardex")]
    public async Task<IActionResult> GetMiKardex(CancellationToken cancellationToken)
    {
        var empleadoId = await ResolveCurrentEmpleadoIdAsync(cancellationToken);
        if (!empleadoId.HasValue)
            return Forbid();

        try
        {
            return Ok(await _vacacionesService.GetKardexAsync(empleadoId.Value, cancellationToken));
        }
        catch (Exception ex)
        {
            return ToActionResult(ex);
        }
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost("Vacaciones/empleado/{empleadoId:int}/generar-periodo")]
    public async Task<IActionResult> GenerarPeriodo(
        int empleadoId,
        [FromBody] VacacionGenerarPeriodoRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _vacacionesService.GenerarPeriodoAsync(
                empleadoId,
                request,
                TryGetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return ToActionResult(ex);
        }
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost("Vacaciones/empleado/{empleadoId:int}/registrar-disfrute")]
    public async Task<IActionResult> RegistrarDisfrute(
        int empleadoId,
        [FromBody] VacacionRegistrarDisfruteRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _vacacionesService.RegistrarDisfruteAsync(
                empleadoId,
                request,
                TryGetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return ToActionResult(ex);
        }
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost("Vacaciones/empleado/{empleadoId:int}/ajuste")]
    public async Task<IActionResult> RegistrarAjuste(
        int empleadoId,
        [FromBody] VacacionAjusteRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _vacacionesService.RegistrarAjusteAsync(
                empleadoId,
                request,
                TryGetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return ToActionResult(ex);
        }
    }

    private async Task<IActionResult?> EnsureCanAccessEmpleadoAsync(
        int empleadoId,
        CancellationToken cancellationToken)
    {
        if (CurrentUserHasAnyRole("ADMIN", "RRHH"))
            return null;

        var linkedEmpleadoId = await ResolveCurrentEmpleadoIdAsync(cancellationToken);

        if (!linkedEmpleadoId.HasValue || linkedEmpleadoId.Value != empleadoId)
            return Forbid();

        return null;
    }

    private bool CurrentUserHasAnyRole(params string[] roles)
    {
        var allowed = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
            return false;

        var currentRoles = User.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role ||
                claim.Type == "role" ||
                claim.Type == "roles" ||
                claim.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(role => role.Trim().ToUpperInvariant())
            .Where(role => !string.IsNullOrWhiteSpace(role));

        return currentRoles.Any(allowed.Contains);
    }

    private async Task<int?> ResolveCurrentEmpleadoIdAsync(CancellationToken cancellationToken)
    {
        var empleadoIdFromClaim = TryGetIntClaimValue(
            "empleadoId",
            "EmpleadoId",
            "empleado_id",
            "rh_empleado_id");

        if (empleadoIdFromClaim.HasValue && empleadoIdFromClaim.Value > 0)
            return empleadoIdFromClaim.Value;

        var userId = TryGetCurrentUserId();
        if (userId.HasValue)
        {
            var empleadoIdByUserId = await _db.Users
                .AsNoTracking()
                .Where(x => x.Id == userId.Value && x.IsActive)
                .Select(x => x.EmpleadoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdByUserId.HasValue && empleadoIdByUserId.Value > 0)
                return empleadoIdByUserId.Value;
        }

        var email = GetCurrentUserEmail();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var empleadoIdByEmail = await _db.Users
                .AsNoTracking()
                .Where(x => x.IsActive && x.Email.ToLower() == normalizedEmail)
                .Select(x => x.EmpleadoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdByEmail.HasValue && empleadoIdByEmail.Value > 0)
                return empleadoIdByEmail.Value;
        }

        return null;
    }

    private int? TryGetCurrentUserId()
    {
        return TryGetIntClaimValue(
            ClaimTypes.NameIdentifier,
            "sub",
            ClaimTypes.Sid,
            "nameid",
            "userId",
            "UserId",
            "uid");
    }

    private int? TryGetIntClaimValue(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = User.FindFirstValue(claimType);
            if (int.TryParse(value, out var parsed) && parsed > 0)
                return parsed;
        }

        return null;
    }

    private string? GetCurrentUserEmail()
    {
        var value =
            User.FindFirstValue(ClaimTypes.Email) ??
            User.FindFirstValue("email") ??
            User.FindFirstValue("preferred_username") ??
            User.FindFirstValue("unique_name") ??
            User.FindFirstValue("upn") ??
            User.FindFirstValue(ClaimTypes.Name) ??
            User.Identity?.Name;

        var normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Contains('@')
            ? normalized
            : null;
    }

    private ActionResult ToActionResult(Exception ex)
    {
        return ex switch
        {
            KeyNotFoundException => NotFound(new { message = ex.Message }),
            InvalidOperationException => BadRequest(new { message = ex.Message }),
            _ => Problem(
                title: "No fue posible procesar la operación de vacaciones.",
                detail: ex.Message)
        };
    }
}
