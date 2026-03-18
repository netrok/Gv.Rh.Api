using Gv.Rh.Application.DTOs.Dashboard;
using Gv.Rh.Domain.Common; // ajusta este using si tus enums viven en otro namespace
using Gv.Rh.Domain.Common.Enums;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly RhDbContext _context;

    public DashboardController(RhDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var monthStart = new DateOnly(utcNow.Year, utcNow.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var empleadosActivos = await _context.Empleados
            .AsNoTracking()
            .CountAsync(x => x.Activo, cancellationToken);

        var sucursalesActivas = await _context.Sucursales
            .AsNoTracking()
            .CountAsync(x => x.Activo, cancellationToken);

        var incidenciasMes = await _context.Incidencias
            .AsNoTracking()
            .CountAsync(
                x => x.FechaInicio >= monthStart && x.FechaInicio < nextMonthStart,
                cancellationToken
            );

        // Traer agrupado por enum y mapear a string ya en memoria
        var incidenciasPorTipoDb = await _context.Incidencias
            .AsNoTracking()
            .Where(x => x.FechaInicio >= monthStart && x.FechaInicio < nextMonthStart)
            .GroupBy(x => x.Tipo)
            .Select(g => new
            {
                Tipo = g.Key,
                Total = g.Count()
            })
            .ToListAsync(cancellationToken);

        var incidenciasPorTipo = incidenciasPorTipoDb
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Tipo.ToString())
            .Select(x => new DashboardCountByDto
            {
                Nombre = x.Tipo.ToString(),
                Total = x.Total
            })
            .ToList();

        var incidenciasPorEstatusDb = await _context.Incidencias
            .AsNoTracking()
            .GroupBy(x => x.Estatus)
            .Select(g => new
            {
                Estatus = g.Key,
                Total = g.Count()
            })
            .ToListAsync(cancellationToken);

        var incidenciasPorEstatus = incidenciasPorEstatusDb
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Estatus.ToString())
            .Select(x => new DashboardCountByDto
            {
                Nombre = x.Estatus.ToString(),
                Total = x.Total
            })
            .ToList();

        var incidenciasPendientes = incidenciasPorEstatus
    .Where(x =>
        string.Equals(x.Nombre, "PENDIENTE", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(x.Nombre, "Pendiente", StringComparison.OrdinalIgnoreCase))
    .Select(x => x.Total)
    .FirstOrDefault();

        var incidenciasRecientesRaw = await (
            from i in _context.Incidencias.AsNoTracking()
            join e in _context.Empleados.AsNoTracking() on i.EmpleadoId equals e.Id
            orderby i.CreatedAtUtc descending
            select new
            {
                i.Id,
                i.EmpleadoId,
                e.NumEmpleado,
                e.Nombres,
                e.ApellidoPaterno,
                e.ApellidoMaterno,
                i.Tipo,
                i.Estatus,
                i.FechaInicio,
                i.FechaFin,
                i.CreatedAtUtc
            })
            .Take(10)
            .ToListAsync(cancellationToken);

        var incidenciasRecientes = incidenciasRecientesRaw
            .Select(x => new DashboardIncidenciaRecienteDto
            {
                Id = x.Id,
                EmpleadoId = x.EmpleadoId,
                NumEmpleado = x.NumEmpleado,
                EmpleadoNombre = BuildFullName(
                    x.Nombres,
                    x.ApellidoPaterno,
                    x.ApellidoMaterno),
                Tipo = x.Tipo.ToString(),
                Estatus = x.Estatus.ToString(),
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();

        var dto = new DashboardDto
        {
            EmpleadosActivos = empleadosActivos,
            SucursalesActivas = sucursalesActivas,
            IncidenciasPendientes = incidenciasPendientes,
            IncidenciasMes = incidenciasMes,
            IncidenciasPorTipo = incidenciasPorTipo,
            IncidenciasPorEstatus = incidenciasPorEstatus,
            IncidenciasRecientes = incidenciasRecientes
        };

        return Ok(dto);
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