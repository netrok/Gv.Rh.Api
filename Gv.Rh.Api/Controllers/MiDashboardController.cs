using Gv.Rh.Application.DTOs.Dashboard;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Common.Enums;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/me/dashboard")]
[Authorize]
[Produces("application/json")]
public sealed class MiDashboardController : ControllerBase
{
    private readonly RhDbContext _context;

    public MiDashboardController(RhDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MiDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MiDashboardDto>> Get(CancellationToken cancellationToken)
    {
        var empleadoId = GetEmpleadoIdFromClaims();

        if (!empleadoId.HasValue)
        {
            return BadRequest(new
            {
                message = "Tu usuario no está vinculado a un empleado."
            });
        }

        var empleado = await _context.Empleados
            .AsNoTracking()
            .Include(x => x.Puesto)
            .Include(x => x.Departamento)
            .Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Id == empleadoId.Value, cancellationToken);

        if (empleado is null)
        {
            return NotFound(new
            {
                message = "No se encontró el empleado vinculado a tu usuario."
            });
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var warningDate = today.AddDays(30);

        var documentos = await _context.EmpleadoDocumentos
            .AsNoTracking()
            .Where(x => x.EmpleadoId == empleado.Id)
            .Select(x => new
            {
                x.Tipo,
                x.FechaVencimiento
            })
            .ToListAsync(cancellationToken);

        var tiposCargados = documentos
            .Select(x => x.Tipo)
            .Distinct()
            .Count();

        var requeridos = Enum.GetValues<TipoDocumentoEmpleado>().Length;

        var porVencer = documentos.Count(x =>
            x.FechaVencimiento.HasValue &&
            x.FechaVencimiento.Value >= today &&
            x.FechaVencimiento.Value <= warningDate);

        var vencidos = documentos.Count(x =>
            x.FechaVencimiento.HasValue &&
            x.FechaVencimiento.Value < today);

        var incidenciasAgrupadas = await _context.Incidencias
            .AsNoTracking()
            .Where(x => x.EmpleadoId == empleado.Id)
            .GroupBy(x => x.Estatus)
            .Select(g => new
            {
                Estatus = g.Key,
                Total = g.Count()
            })
            .ToListAsync(cancellationToken);

        var pendientes = incidenciasAgrupadas
            .Where(x => x.Estatus == EstatusIncidencia.PENDIENTE)
            .Select(x => x.Total)
            .FirstOrDefault();

        var aprobadas = incidenciasAgrupadas
            .Where(x => x.Estatus == EstatusIncidencia.APROBADA)
            .Select(x => x.Total)
            .FirstOrDefault();

        var rechazadas = incidenciasAgrupadas
            .Where(x => x.Estatus == EstatusIncidencia.RECHAZADA)
            .Select(x => x.Total)
            .FirstOrDefault();

        var dto = new MiDashboardDto
        {
            Empleado = new MiDashboardEmpleadoDto
            {
                Id = empleado.Id,
                NumEmpleado = empleado.NumEmpleado,
                NombreCompleto = BuildFullName(
                    empleado.Nombres,
                    empleado.ApellidoPaterno,
                    empleado.ApellidoMaterno),
                PuestoNombre = empleado.Puesto?.Nombre,
                DepartamentoNombre = empleado.Departamento?.Nombre,
                SucursalNombre = empleado.Sucursal?.Nombre,
                Activo = empleado.Activo
            },
            Documentos = new MiDashboardDocumentosDto
            {
                Requeridos = requeridos,
                Cargados = tiposCargados,
                Faltantes = Math.Max(requeridos - tiposCargados, 0),
                PorVencer = porVencer,
                Vencidos = vencidos,
                Completo = tiposCargados >= requeridos && vencidos == 0
            },
            Incidencias = new MiDashboardIncidenciasDto
            {
                Total = pendientes + aprobadas + rechazadas,
                Pendientes = pendientes,
                Aprobadas = aprobadas,
                Rechazadas = rechazadas
            }
        };

        return Ok(dto);
    }

    private int? GetEmpleadoIdFromClaims()
    {
        var rawValue =
            User.FindFirstValue("empleado_id") ??
            User.FindFirstValue("empleadoId");

        return int.TryParse(rawValue, out var empleadoId)
            ? empleadoId
            : null;
    }

    private static string BuildFullName(
        string nombres,
        string apellidoPaterno,
        string? apellidoMaterno)
    {
        return string.Join(" ", new[]
        {
            nombres?.Trim(),
            apellidoPaterno?.Trim(),
            apellidoMaterno?.Trim()
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}