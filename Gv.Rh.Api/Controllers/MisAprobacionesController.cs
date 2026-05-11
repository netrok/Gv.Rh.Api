using Gv.Rh.Application.DTOs.Aprobaciones;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Common.Enums;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN,RRHH,JEFE")]
[Route("api/me/aprobaciones")]
[Produces("application/json")]
public sealed class MisAprobacionesController : ControllerBase
{
    private const int MaxItems = 100;

    private readonly RhDbContext _db;
    private readonly IEmpleadoAccessScopeService _empleadoAccessScopeService;

    public MisAprobacionesController(
        RhDbContext db,
        IEmpleadoAccessScopeService empleadoAccessScopeService)
    {
        _db = db;
        _empleadoAccessScopeService = empleadoAccessScopeService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MisAprobacionesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MisAprobacionesDto>> GetMisAprobaciones(
        CancellationToken cancellationToken)
    {
        var isAdminOrRrhh = _empleadoAccessScopeService.IsAdminOrRrhh(User);
        var isJefe = _empleadoAccessScopeService.IsJefe(User);

        int? currentEmpleadoId = null;

        if (!isAdminOrRrhh)
        {
            if (!isJefe)
            {
                return Forbid();
            }

            currentEmpleadoId = await _empleadoAccessScopeService.ResolveCurrentEmpleadoIdAsync(
                User,
                cancellationToken);

            if (!currentEmpleadoId.HasValue || currentEmpleadoId.Value <= 0)
            {
                return Forbid();
            }
        }

        var incidenciasQuery = _db.Incidencias
            .AsNoTracking()
            .Where(x => x.Estatus == EstatusIncidencia.PENDIENTE);

        var vacacionesQuery = _db.VacacionSolicitudes
            .AsNoTracking()
            .Where(x => x.Estatus == EstatusVacacionSolicitud.PENDIENTE);

        if (!isAdminOrRrhh)
        {
            var jefeEmpleadoId = currentEmpleadoId!.Value;

            incidenciasQuery = incidenciasQuery.Where(x =>
                x.Empleado.AprobadorPrimarioEmpleadoId == jefeEmpleadoId ||
                x.Empleado.AprobadorSecundarioEmpleadoId == jefeEmpleadoId);

            vacacionesQuery = vacacionesQuery.Where(x =>
                x.Empleado != null &&
                (
                    x.Empleado.AprobadorPrimarioEmpleadoId == jefeEmpleadoId ||
                    x.Empleado.AprobadorSecundarioEmpleadoId == jefeEmpleadoId
                ));
        }

        var incidenciasPendientes = await incidenciasQuery.CountAsync(cancellationToken);
        var vacacionesPendientes = await vacacionesQuery.CountAsync(cancellationToken);

        var incidenciaRows = await incidenciasQuery
            .OrderBy(x => x.FechaInicio)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.EmpleadoId,
                x.Empleado.NumEmpleado,
                x.Empleado.Nombres,
                x.Empleado.ApellidoPaterno,
                x.Empleado.ApellidoMaterno,
                x.Tipo,
                x.FechaInicio,
                x.FechaFin,
                x.Estatus,
                x.CreatedAtUtc
            })
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        var vacacionesRows = await vacacionesQuery
            .OrderBy(x => x.FechaInicio)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.EmpleadoId,
                NumEmpleado = x.Empleado != null ? x.Empleado.NumEmpleado : string.Empty,
                Nombres = x.Empleado != null ? x.Empleado.Nombres : string.Empty,
                ApellidoPaterno = x.Empleado != null ? x.Empleado.ApellidoPaterno : string.Empty,
                ApellidoMaterno = x.Empleado != null ? x.Empleado.ApellidoMaterno : null,
                x.FechaInicio,
                x.FechaFin,
                x.DiasSolicitados,
                x.Estatus,
                x.CreatedAtUtc
            })
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        var incidenciaItems = incidenciaRows
            .Select(x => new MisAprobacionesItemDto
            {
                Tipo = "INCIDENCIA",
                Id = x.Id,
                EmpleadoId = x.EmpleadoId,
                NumEmpleado = x.NumEmpleado,
                EmpleadoNombre = BuildNombreCompleto(
                    x.Nombres,
                    x.ApellidoPaterno,
                    x.ApellidoMaterno),
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Estatus = x.Estatus.ToString(),
                Descripcion = $"Incidencia: {FormatLabel(x.Tipo.ToString())}",
                UrlDetalle = "/incidencias",
                CreatedAtUtc = x.CreatedAtUtc
            });

        var vacacionesItems = vacacionesRows
            .Select(x => new MisAprobacionesItemDto
            {
                Tipo = "VACACIONES",
                Id = x.Id,
                EmpleadoId = x.EmpleadoId,
                NumEmpleado = x.NumEmpleado,
                EmpleadoNombre = BuildNombreCompleto(
                    x.Nombres,
                    x.ApellidoPaterno,
                    x.ApellidoMaterno),
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Estatus = x.Estatus.ToString(),
                Descripcion = $"Solicitud de vacaciones: {x.DiasSolicitados:0.##} día(s)",
                UrlDetalle = "/vacaciones/solicitudes",
                CreatedAtUtc = x.CreatedAtUtc
            });

        var items = incidenciaItems
            .Concat(vacacionesItems)
            .OrderBy(x => x.FechaInicio)
            .ThenBy(x => x.Tipo)
            .ThenBy(x => x.Id)
            .Take(MaxItems)
            .ToList();

        var dto = new MisAprobacionesDto
        {
            IncidenciasPendientes = incidenciasPendientes,
            VacacionesPendientes = vacacionesPendientes,
            TotalPendientes = incidenciasPendientes + vacacionesPendientes,
            Items = items
        };

        return Ok(dto);
    }

    private static string BuildNombreCompleto(
        string? nombres,
        string? apellidoPaterno,
        string? apellidoMaterno)
    {
        return string.Join(
            " ",
            new[]
            {
                nombres,
                apellidoPaterno,
                apellidoMaterno
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string FormatLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("_", " ")
            .ToLowerInvariant();
    }
}
