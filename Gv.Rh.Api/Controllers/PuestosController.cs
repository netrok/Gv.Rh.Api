using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Puestos;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public class PuestosController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly IPuestosReportService _puestosReportService;

    public PuestosController(
        RhDbContext db,
        IPuestosReportService puestosReportService)
    {
        _db = db;
        _puestosReportService = puestosReportService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] bool? activo = null,
        [FromQuery] int? departamentoId = null,
        [FromQuery] string sort = "nombre",
        [FromQuery] string dir = "asc")
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<Puesto> query = _db.Puestos
            .AsNoTracking()
            .Include(x => x.Departamento);

        if (activo.HasValue)
            query = query.Where(x => x.Activo == activo.Value);

        if (departamentoId.HasValue)
            query = query.Where(x => x.DepartamentoId == departamentoId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Clave, $"%{q}%") ||
                EF.Functions.ILike(x.Nombre, $"%{q}%") ||
                EF.Functions.ILike(x.Departamento!.Nombre, $"%{q}%"));
        }

        bool asc = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);

        query = (sort?.ToLowerInvariant()) switch
        {
            "clave" => asc ? query.OrderBy(x => x.Clave) : query.OrderByDescending(x => x.Clave),
            "departamento" => asc ? query.OrderBy(x => x.Departamento!.Nombre) : query.OrderByDescending(x => x.Departamento!.Nombre),
            "activo" => asc ? query.OrderBy(x => x.Activo) : query.OrderByDescending(x => x.Activo),
            "createdatutc" or "createdat" => asc ? query.OrderBy(x => x.CreatedAtUtc) : query.OrderByDescending(x => x.CreatedAtUtc),
            "updatedatutc" or "updatedat" => asc ? query.OrderBy(x => x.UpdatedAtUtc) : query.OrderByDescending(x => x.UpdatedAtUtc),
            _ => asc ? query.OrderBy(x => x.Nombre) : query.OrderByDescending(x => x.Nombre),
        };

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PuestoDto
            {
                Id = x.Id,
                Clave = x.Clave,
                Nombre = x.Nombre,
                DepartamentoId = x.DepartamentoId,
                DepartamentoNombre = x.Departamento != null ? x.Departamento.Nombre : string.Empty,
                Activo = x.Activo,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            items
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _db.Puestos
            .AsNoTracking()
            .Include(x => x.Departamento)
            .Where(x => x.Id == id)
            .Select(x => new PuestoDto
            {
                Id = x.Id,
                Clave = x.Clave,
                Nombre = x.Nombre,
                DepartamentoId = x.DepartamentoId,
                DepartamentoNombre = x.Departamento != null ? x.Departamento.Nombre : string.Empty,
                Activo = x.Activo,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync();

        return item is null
            ? NotFound(new { message = "Puesto no existe." })
            : Ok(item);
    }

    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] PuestoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _puestosReportService.BuildXlsxAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] PuestoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _puestosReportService.BuildPdfAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PuestoCreateDto dto)
    {
        var clave = dto.Clave.Trim().ToUpperInvariant();
        var nombre = dto.Nombre.Trim();

        var departamento = await _db.Departamentos
            .FirstOrDefaultAsync(x => x.Id == dto.DepartamentoId && x.Activo);

        if (departamento is null)
            return BadRequest(new { message = "El departamento indicado no existe o está inactivo." });

        var existsByClave = await _db.Puestos.AnyAsync(x => x.Clave == clave);
        if (existsByClave)
            return Conflict(new { message = "Ya existe un puesto con esa clave." });

        var entity = new Puesto
        {
            Clave = clave,
            Nombre = nombre,
            DepartamentoId = dto.DepartamentoId,
            Activo = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Puestos.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new PuestoDto
        {
            Id = entity.Id,
            Clave = entity.Clave,
            Nombre = entity.Nombre,
            DepartamentoId = entity.DepartamentoId,
            DepartamentoNombre = departamento.Nombre,
            Activo = entity.Activo,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PuestoUpdateDto dto)
    {
        var entity = await _db.Puestos.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound(new { message = "Puesto no existe." });

        var departamento = await _db.Departamentos
            .FirstOrDefaultAsync(x => x.Id == dto.DepartamentoId && x.Activo);

        if (departamento is null)
            return BadRequest(new { message = "El departamento indicado no existe o está inactivo." });

        var clave = dto.Clave.Trim().ToUpperInvariant();
        var nombre = dto.Nombre.Trim();

        var existsByClave = await _db.Puestos.AnyAsync(x => x.Id != id && x.Clave == clave);
        if (existsByClave)
            return Conflict(new { message = "Ya existe otro puesto con esa clave." });

        if (!dto.Activo && entity.Activo)
        {
            var validationError = await ValidateCanDeactivateAsync(id);
            if (validationError is not null)
                return validationError;
        }

        entity.Clave = clave;
        entity.Nombre = nombre;
        entity.DepartamentoId = dto.DepartamentoId;
        entity.Activo = dto.Activo;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new PuestoDto
        {
            Id = entity.Id,
            Clave = entity.Clave,
            Nombre = entity.Nombre,
            DepartamentoId = entity.DepartamentoId,
            DepartamentoNombre = departamento.Nombre,
            Activo = entity.Activo,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Puestos.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound(new { message = "Puesto no existe." });

        if (!entity.Activo)
            return NoContent();

        var validationError = await ValidateCanDeactivateAsync(id);
        if (validationError is not null)
            return validationError;

        entity.Activo = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var entity = await _db.Puestos.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound(new { message = "Puesto no existe." });

        if (entity.Activo)
            return NoContent();

        entity.Activo = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult?> ValidateCanDeactivateAsync(int puestoId)
    {
        var tieneEmpleados = await _db.Empleados
            .AsNoTracking()
            .AnyAsync(x => x.PuestoId == puestoId);

        if (tieneEmpleados)
        {
            return Conflict(new
            {
                message = "No se puede desactivar el puesto porque tiene empleados relacionados."
            });
        }

        return null;
    }
}