using Gv.Rh.Api.DTOs.Sucursales;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Sucursales;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SucursalesController : ControllerBase
{
    private readonly RhDbContext _context;
    private readonly ISucursalesReportService _sucursalesReportService;

    public SucursalesController(
        RhDbContext context,
        ISucursalesReportService sucursalesReportService)
    {
        _context = context;
        _sucursalesReportService = sucursalesReportService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SucursalDto>>> GetAll(
        [FromQuery] bool? activo = null,
        [FromQuery] string? q = null)
    {
        var query = _context.Sucursales
            .AsNoTracking()
            .Include(x => x.Empleados)
            .AsQueryable();

        if (activo.HasValue)
            query = query.Where(x => x.Activo == activo.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Clave, $"%{q}%") ||
                EF.Functions.ILike(x.Nombre, $"%{q}%") ||
                (x.Direccion != null && EF.Functions.ILike(x.Direccion, $"%{q}%")) ||
                (x.Telefono != null && EF.Functions.ILike(x.Telefono, $"%{q}%")));
        }

        var items = await query
            .OrderBy(x => x.Nombre)
            .Select(x => new SucursalDto
            {
                Id = x.Id,
                Clave = x.Clave,
                Nombre = x.Nombre,
                Direccion = x.Direccion,
                Telefono = x.Telefono,
                Activo = x.Activo,
                EmpleadosActivos = x.Empleados.Count(e => e.Activo)
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SucursalDto>> GetById(int id)
    {
        var item = await _context.Sucursales
            .AsNoTracking()
            .Include(x => x.Empleados)
            .Where(x => x.Id == id)
            .Select(x => new SucursalDto
            {
                Id = x.Id,
                Clave = x.Clave,
                Nombre = x.Nombre,
                Direccion = x.Direccion,
                Telefono = x.Telefono,
                Activo = x.Activo,
                EmpleadosActivos = x.Empleados.Count(e => e.Activo)
            })
            .FirstOrDefaultAsync();

        if (item is null)
            return NotFound();

        return Ok(item);
    }

    [HttpGet("export/xlsx")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] SucursalReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _sucursalesReportService.BuildXlsxAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpGet("export/pdf")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] SucursalReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _sucursalesReportService.BuildPdfAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<ActionResult<SucursalDto>> Create(SucursalCreateDto dto)
    {
        var clave = dto.Clave.Trim().ToUpperInvariant();
        var nombre = dto.Nombre.Trim();

        var claveExiste = await _context.Sucursales
            .AnyAsync(x => x.Clave == clave);

        if (claveExiste)
            return BadRequest("Ya existe una sucursal con esa clave.");

        var entity = new Sucursal
        {
            Clave = clave,
            Nombre = nombre,
            Direccion = string.IsNullOrWhiteSpace(dto.Direccion) ? null : dto.Direccion.Trim(),
            Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
            Activo = dto.Activo
        };

        _context.Sucursales.Add(entity);
        await _context.SaveChangesAsync();

        var result = new SucursalDto
        {
            Id = entity.Id,
            Clave = entity.Clave,
            Nombre = entity.Nombre,
            Direccion = entity.Direccion,
            Telefono = entity.Telefono,
            Activo = entity.Activo,
            EmpleadosActivos = 0
        };

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<ActionResult<SucursalDto>> Update(int id, SucursalUpdateDto dto)
    {
        var entity = await _context.Sucursales
            .Include(x => x.Empleados)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return NotFound();

        var clave = dto.Clave.Trim().ToUpperInvariant();
        var nombre = dto.Nombre.Trim();

        var claveDuplicada = await _context.Sucursales
            .AnyAsync(x => x.Id != id && x.Clave == clave);

        if (claveDuplicada)
            return BadRequest("Ya existe otra sucursal con esa clave.");

        if (!dto.Activo && entity.Empleados.Any(e => e.Activo))
            return BadRequest("No puedes desactivar una sucursal con empleados activos asignados.");

        entity.Clave = clave;
        entity.Nombre = nombre;
        entity.Direccion = string.IsNullOrWhiteSpace(dto.Direccion) ? null : dto.Direccion.Trim();
        entity.Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();
        entity.Activo = dto.Activo;

        await _context.SaveChangesAsync();

        return Ok(new SucursalDto
        {
            Id = entity.Id,
            Clave = entity.Clave,
            Nombre = entity.Nombre,
            Direccion = entity.Direccion,
            Telefono = entity.Telefono,
            Activo = entity.Activo,
            EmpleadosActivos = entity.Empleados.Count(e => e.Activo)
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.Sucursales
            .Include(x => x.Empleados)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return NotFound();

        if (entity.Empleados.Any(e => e.Activo))
            return BadRequest("No puedes eliminar/desactivar una sucursal con empleados activos asignados.");

        entity.Activo = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}