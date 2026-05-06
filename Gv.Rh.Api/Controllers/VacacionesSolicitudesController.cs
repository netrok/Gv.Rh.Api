using Gv.Rh.Application.DTOs.Vacaciones.Solicitudes;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/Vacaciones/solicitudes")]
[Produces("application/json")]
public sealed class VacacionesSolicitudesController : ControllerBase
{
    private readonly IVacacionesSolicitudesService _solicitudesService;
    private readonly RhDbContext _db;

    public VacacionesSolicitudesController(
        IVacacionesSolicitudesService solicitudesService,
        RhDbContext db)
    {
        _solicitudesService = solicitudesService;
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(VacacionesSolicitudListResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesSolicitudListResultDto>> GetAll(
        [FromQuery] VacacionesSolicitudQueryDto query,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        var result = await _solicitudesService.GetSolicitudesAsync(query, actor, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VacacionesSolicitudDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesSolicitudDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);
        var result = await _solicitudesService.GetSolicitudByIdAsync(id, actor, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(VacacionesSolicitudDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<VacacionesSolicitudDto>> Create(
        [FromBody] VacacionesSolicitudCreateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var result = await _solicitudesService.CreateSolicitudAsync(request, actor, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "ADMIN,RRHH,JEFE")]
    [HttpPost("{id:int}/aprobar")]
    [ProducesResponseType(typeof(VacacionesSolicitudDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesSolicitudDto>> Aprobar(
        int id,
        [FromBody] VacacionesSolicitudResolverDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var result = await _solicitudesService.AprobarSolicitudAsync(id, request, actor, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "ADMIN,RRHH,JEFE")]
    [HttpPost("{id:int}/rechazar")]
    [ProducesResponseType(typeof(VacacionesSolicitudDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesSolicitudDto>> Rechazar(
        int id,
        [FromBody] VacacionesSolicitudResolverDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var result = await _solicitudesService.RechazarSolicitudAsync(id, request, actor, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/cancelar")]
    [ProducesResponseType(typeof(VacacionesSolicitudDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesSolicitudDto>> Cancelar(
        int id,
        [FromBody] VacacionesSolicitudResolverDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);
            var result = await _solicitudesService.CancelarSolicitudAsync(id, request, actor, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("/api/me/vacaciones/solicitudes")]
    [ProducesResponseType(typeof(VacacionesSolicitudListResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesSolicitudListResultDto>> GetMisSolicitudes(
        [FromQuery] VacacionesSolicitudQueryDto query,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActorAsync(cancellationToken);

        if (!actor.EmpleadoId.HasValue)
            return Forbid();

        query.EmpleadoId = actor.EmpleadoId.Value;

        var result = await _solicitudesService.GetSolicitudesAsync(query, actor, cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/me/vacaciones/solicitudes")]
    [ProducesResponseType(typeof(VacacionesSolicitudDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<VacacionesSolicitudDto>> CreateMiSolicitud(
        [FromBody] VacacionesSolicitudCreateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = await ResolveActorAsync(cancellationToken);

            if (!actor.EmpleadoId.HasValue)
                return Forbid();

            request.EmpleadoId = actor.EmpleadoId.Value;

            var result = await _solicitudesService.CreateSolicitudAsync(request, actor, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<VacacionesSolicitudActorDto> ResolveActorAsync(
        CancellationToken cancellationToken)
    {
        var email =
            User.FindFirstValue(ClaimTypes.Email) ??
            User.FindFirstValue("email") ??
            User.FindFirstValue(ClaimTypes.Name) ??
            User.Identity?.Name;

        var role =
            User.FindFirstValue(ClaimTypes.Role) ??
            User.FindFirstValue("role") ??
            User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ??
            string.Empty;

        var userId = TryGetIntClaimValue(
            ClaimTypes.NameIdentifier,
            "sub",
            "uid",
            "userId",
            "UserId",
            "id");

        int? empleadoIdFromClaim = TryGetIntClaimValue(
            "empleadoId",
            "EmpleadoId",
            "employeeId",
            "EmployeeId");

        var userQuery = _db.Users.AsNoTracking();

        if (userId.HasValue)
        {
            var userById = await userQuery
                .Where(x => x.Id == userId.Value)
                .Select(x => new
                {
                    x.Id,
                    x.Email,
                    x.Role,
                    x.EmpleadoId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (userById is not null)
            {
                return new VacacionesSolicitudActorDto
                {
                    UsuarioId = userById.Id,
                    UsuarioEmail = userById.Email,
                    Role = string.IsNullOrWhiteSpace(role) ? userById.Role : role,
                    EmpleadoId = empleadoIdFromClaim ?? userById.EmpleadoId
                };
            }
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLower();

            var userByEmail = await userQuery
                .Where(x => x.Email.ToLower() == normalizedEmail)
                .Select(x => new
                {
                    x.Id,
                    x.Email,
                    x.Role,
                    x.EmpleadoId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (userByEmail is not null)
            {
                return new VacacionesSolicitudActorDto
                {
                    UsuarioId = userByEmail.Id,
                    UsuarioEmail = userByEmail.Email,
                    Role = string.IsNullOrWhiteSpace(role) ? userByEmail.Role : role,
                    EmpleadoId = empleadoIdFromClaim ?? userByEmail.EmpleadoId
                };
            }
        }

        return new VacacionesSolicitudActorDto
        {
            UsuarioId = userId,
            UsuarioEmail = email,
            Role = role,
            EmpleadoId = empleadoIdFromClaim
        };
    }

    private int? TryGetIntClaimValue(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = User.FindFirstValue(claimType);

            if (int.TryParse(value, out var parsed))
                return parsed;
        }

        return null;
    }
}