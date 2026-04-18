using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Departamentos;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public class DepartamentosController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly IDepartamentosReportService _departamentosReportService;

    public DepartamentosController(
        RhDbContext db,
        IDepartamentosReportService departamentosReportService)
    {
        _db = db;
        _departamentosReportService = departamentosReportService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] bool? activo = null,
        [FromQuery] string sort = "nombre",
        [FromQuery] string dir = "asc")
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<Departamento> query = _db.Departamentos.AsNoTracking();

        if (activo.HasValue)
            query = query.Where(x => x.Activo == activo.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Clave, $"%{q}%") ||
                EF.Functions.ILike(x.Nombre, $"%{q}%"));
        }

        bool asc = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);

        query = (sort?.ToLowerInvariant()) switch
        {
            "clave" => asc ? query.OrderBy(x => x.Clave) : query.OrderByDescending(x => x.Clave),
            "activo" => asc ? query.OrderBy(x => x.Activo) : query.OrderByDescending(x => x.Activo),
            "createdatutc" or "createdat" => asc ? query.OrderBy(x => x.CreatedAtUtc) : query.OrderByDescending(x => x.CreatedAtUtc),
            "updatedatutc" or "updatedat" => asc ? query.OrderBy(x => x.UpdatedAtUtc) : query.OrderByDescending(x => x.UpdatedAtUtc),
            _ => asc ? query.OrderBy(x => x.Nombre) : query.OrderByDescending(x => x.Nombre),
        };

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new DepartamentoDto
            {
                Id = x.Id,
                Clave = x.Clave,
                Nombre = x.Nombre,
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
        var item = await _db.Departamentos
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new DepartamentoDto
            {
                Id = x.Id,
                Clave = x.Clave,
                Nombre = x.Nombre,
                Activo = x.Activo,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync();

        return item is null
            ? NotFound(new { message = "Departamento no existe." })
            : Ok(item);
    }

    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] DepartamentoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _departamentosReportService.BuildXlsxAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] DepartamentoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _departamentosReportService.BuildPdfAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DepartamentoCreateDto dto)
    {
        var clave = dto.Clave.Trim().ToUpperInvariant();
        var nombre = dto.Nombre.Trim();

        var existsByClave = await _db.Departamentos.AnyAsync(x => x.Clave == clave);
        if (existsByClave)
            return Conflict(new { message = "Ya existe un departamento con esa clave." });

        var entity = new Departamento
        {
            Clave = clave,
            Nombre = nombre,
            Activo = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Departamentos.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new DepartamentoDto
        {
            Id = entity.Id,
            Clave = entity.Clave,
            Nombre = entity.Nombre,
            Activo = entity.Activo,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] DepartamentoUpdateDto dto)
    {
        var entity = await _db.Departamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound(new { message = "Departamento no existe." });

        var clave = dto.Clave.Trim().ToUpperInvariant();
        var nombre = dto.Nombre.Trim();

        var existsByClave = await _db.Departamentos.AnyAsync(x => x.Id != id && x.Clave == clave);
        if (existsByClave)
            return Conflict(new { message = "Ya existe otro departamento con esa clave." });

        if (!dto.Activo && entity.Activo)
        {
            var validationError = await ValidateCanDeactivateAsync(id);
            if (validationError is not null)
                return validationError;
        }

        entity.Clave = clave;
        entity.Nombre = nombre;
        entity.Activo = dto.Activo;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new DepartamentoDto
        {
            Id = entity.Id,
            Clave = entity.Clave,
            Nombre = entity.Nombre,
            Activo = entity.Activo,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Departamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound(new { message = "Departamento no existe." });

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
        var entity = await _db.Departamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound(new { message = "Departamento no existe." });

        if (entity.Activo)
            return NoContent();

        entity.Activo = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult?> ValidateCanDeactivateAsync(int departamentoId)
    {
        var tienePuestosActivos = await _db.Puestos
            .AsNoTracking()
            .AnyAsync(x => x.DepartamentoId == departamentoId && x.Activo);

        if (tienePuestosActivos)
        {
            return Conflict(new
            {
                message = "No se puede desactivar el departamento porque tiene puestos activos relacionados."
            });
        }

        var tieneEmpleados = await _db.Empleados
            .AsNoTracking()
            .AnyAsync(x => x.DepartamentoId == departamentoId);

        if (tieneEmpleados)
        {
            return Conflict(new
            {
                message = "No se puede desactivar el departamento porque tiene empleados relacionados."
            });
        }

        return null;
    }
}