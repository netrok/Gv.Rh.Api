using Gv.Rh.Application.DTOs.Incidencias;
using Gv.Rh.Domain.Common.Enums;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncidenciasController : ControllerBase
{
    private readonly RhDbContext _context;

    public IncidenciasController(RhDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<IncidenciaDto>>> GetAll([FromQuery] IncidenciaQueryDto query)
    {
        var incidenciasQuery = _context.Incidencias
            .AsNoTracking()
            .Include(x => x.Empleado)
            .Include(x => x.Sucursal)
            .AsQueryable();

        if (query.EmpleadoId.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.EmpleadoId == query.EmpleadoId.Value);

        if (query.SucursalId.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.SucursalId == query.SucursalId.Value);

        if (query.Tipo.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.Tipo == query.Tipo.Value);

        if (query.Estatus.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.Estatus == query.Estatus.Value);

        if (query.SoloPendientes == true)
            incidenciasQuery = incidenciasQuery.Where(x => x.Estatus == EstatusIncidencia.PENDIENTE);

        if (query.FechaDesde.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.FechaInicio >= query.FechaDesde.Value);

        if (query.FechaHasta.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.FechaFin <= query.FechaHasta.Value);

        var items = await incidenciasQuery
            .OrderByDescending(x => x.FechaInicio)
            .ThenByDescending(x => x.Id)
            .Select(x => new IncidenciaDto
            {
                Id = x.Id,
                EmpleadoId = x.EmpleadoId,
                EmpleadoNombre = x.Empleado.Nombres + " " + x.Empleado.ApellidoPaterno +
                                 (string.IsNullOrWhiteSpace(x.Empleado.ApellidoMaterno) ? "" : " " + x.Empleado.ApellidoMaterno),
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Tipo = x.Tipo,
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Comentario = x.Comentario,
                Estatus = x.Estatus,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IncidenciaDto>> GetById(int id)
    {
        var item = await _context.Incidencias
            .AsNoTracking()
            .Include(x => x.Empleado)
            .Include(x => x.Sucursal)
            .Where(x => x.Id == id)
            .Select(x => new IncidenciaDto
            {
                Id = x.Id,
                EmpleadoId = x.EmpleadoId,
                EmpleadoNombre = x.Empleado.Nombres + " " + x.Empleado.ApellidoPaterno +
                                 (string.IsNullOrWhiteSpace(x.Empleado.ApellidoMaterno) ? "" : " " + x.Empleado.ApellidoMaterno),
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Tipo = x.Tipo,
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Comentario = x.Comentario,
                Estatus = x.Estatus,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync();

        if (item is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<ActionResult<IncidenciaDto>> Create([FromBody] CreateIncidenciaDto dto)
    {
        if (dto.FechaFin < dto.FechaInicio)
            return BadRequest(new { message = "La fecha fin no puede ser menor que la fecha inicio." });

        var empleado = await _context.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.EmpleadoId);

        if (empleado is null)
            return BadRequest(new { message = "El empleado no existe." });

        if (!empleado.Activo)
            return BadRequest(new { message = "No se puede crear una incidencia para un empleado inactivo." });

        int? sucursalId = dto.SucursalId ?? empleado.SucursalId;

        if (sucursalId.HasValue)
        {
            var sucursalExiste = await _context.Sucursales
                .AsNoTracking()
                .AnyAsync(x => x.Id == sucursalId.Value);

            if (!sucursalExiste)
                return BadRequest(new { message = "La sucursal indicada no existe." });
        }

        var entity = new Incidencia
        {
            EmpleadoId = dto.EmpleadoId,
            SucursalId = sucursalId,
            Tipo = dto.Tipo,
            FechaInicio = dto.FechaInicio,
            FechaFin = dto.FechaFin,
            Comentario = dto.Comentario,
            Estatus = EstatusIncidencia.PENDIENTE,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.Incidencias.Add(entity);
        await _context.SaveChangesAsync();

        var created = await _context.Incidencias
            .AsNoTracking()
            .Include(x => x.Empleado)
            .Include(x => x.Sucursal)
            .Where(x => x.Id == entity.Id)
            .Select(x => new IncidenciaDto
            {
                Id = x.Id,
                EmpleadoId = x.EmpleadoId,
                EmpleadoNombre = x.Empleado.Nombres + " " + x.Empleado.ApellidoPaterno +
                                 (string.IsNullOrWhiteSpace(x.Empleado.ApellidoMaterno) ? "" : " " + x.Empleado.ApellidoMaterno),
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Tipo = x.Tipo,
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Comentario = x.Comentario,
                Estatus = x.Estatus,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstAsync();

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<ActionResult<IncidenciaDto>> Update(int id, [FromBody] UpdateIncidenciaDto dto)
    {
        if (dto.FechaFin < dto.FechaInicio)
            return BadRequest(new { message = "La fecha fin no puede ser menor que la fecha inicio." });

        var entity = await _context.Incidencias.FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        if (entity.Estatus != EstatusIncidencia.PENDIENTE)
            return BadRequest(new { message = "Solo se pueden editar incidencias en estatus PENDIENTE." });

        var empleado = await _context.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.EmpleadoId);

        if (empleado is null)
            return BadRequest(new { message = "El empleado no existe." });

        if (!empleado.Activo)
            return BadRequest(new { message = "No se puede asignar una incidencia a un empleado inactivo." });

        int? sucursalId = dto.SucursalId ?? empleado.SucursalId;

        if (sucursalId.HasValue)
        {
            var sucursalExiste = await _context.Sucursales
                .AsNoTracking()
                .AnyAsync(x => x.Id == sucursalId.Value);

            if (!sucursalExiste)
                return BadRequest(new { message = "La sucursal indicada no existe." });
        }

        entity.EmpleadoId = dto.EmpleadoId;
        entity.SucursalId = sucursalId;
        entity.Tipo = dto.Tipo;
        entity.FechaInicio = dto.FechaInicio;
        entity.FechaFin = dto.FechaFin;
        entity.Comentario = dto.Comentario;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var updated = await _context.Incidencias
            .AsNoTracking()
            .Include(x => x.Empleado)
            .Include(x => x.Sucursal)
            .Where(x => x.Id == entity.Id)
            .Select(x => new IncidenciaDto
            {
                Id = x.Id,
                EmpleadoId = x.EmpleadoId,
                EmpleadoNombre = x.Empleado.Nombres + " " + x.Empleado.ApellidoPaterno +
                                 (string.IsNullOrWhiteSpace(x.Empleado.ApellidoMaterno) ? "" : " " + x.Empleado.ApellidoMaterno),
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Tipo = x.Tipo,
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Comentario = x.Comentario,
                Estatus = x.Estatus,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstAsync();

        return Ok(updated);
    }

    [HttpPost("{id:int}/aprobar")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> Aprobar(int id)
    {
        var entity = await _context.Incidencias.FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        if (entity.Estatus != EstatusIncidencia.PENDIENTE)
            return BadRequest(new { message = "Solo se pueden aprobar incidencias en estatus PENDIENTE." });

        entity.Estatus = EstatusIncidencia.APROBADA;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Incidencia aprobada correctamente." });
    }

    [HttpPost("{id:int}/rechazar")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> Rechazar(int id)
    {
        var entity = await _context.Incidencias.FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        if (entity.Estatus != EstatusIncidencia.PENDIENTE)
            return BadRequest(new { message = "Solo se pueden rechazar incidencias en estatus PENDIENTE." });

        entity.Estatus = EstatusIncidencia.RECHAZADA;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Incidencia rechazada correctamente." });
    }

    [HttpGet("catalogos/tipos")]
    public ActionResult<IEnumerable<object>> GetTipos()
    {
        var items = Enum.GetValues<TipoIncidencia>()
            .Select(x => new
            {
                id = (int)x,
                clave = x.ToString(),
                nombre = x.ToString().Replace("_", " ")
            });

        return Ok(items);
    }

    [HttpGet("catalogos/estatus")]
    public ActionResult<IEnumerable<object>> GetEstatus()
    {
        var items = Enum.GetValues<EstatusIncidencia>()
            .Select(x => new
            {
                id = (int)x,
                clave = x.ToString(),
                nombre = x.ToString().Replace("_", " ")
            });

        return Ok(items);
    }
}