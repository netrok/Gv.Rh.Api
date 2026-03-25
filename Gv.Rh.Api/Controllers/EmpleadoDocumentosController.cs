using Gv.Rh.Api.Services;
using Gv.Rh.Application.DTOs.EmpleadosDocumentos;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gv.Rh.Api.Contracts.EmpleadoDocumentos;


namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN,RRHH")]
[Route("api")]
public class EmpleadoDocumentosController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly IEmpleadoDocumentoStorageService _storage;

    public EmpleadoDocumentosController(
        RhDbContext db,
        IEmpleadoDocumentoStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet("Empleados/{empleadoId:int}/documentos")]
    public async Task<ActionResult<List<EmpleadoDocumentoDto>>> GetByEmpleado(
        int empleadoId,
        CancellationToken cancellationToken)
    {
        var empleadoExiste = await _db.Empleados
            .AsNoTracking()
            .AnyAsync(x => x.Id == empleadoId, cancellationToken);

        if (!empleadoExiste)
            return NotFound(new { message = "El empleado no existe." });

        var items = await _db.EmpleadoDocumentos
            .AsNoTracking()
            .Where(x => x.EmpleadoId == empleadoId && x.Activo)
            .OrderBy(x => x.Tipo)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("Empleados/{empleadoId:int}/documentos")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<EmpleadoDocumentoDto>> Create(
        int empleadoId,
        [FromForm] EmpleadoDocumentoCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var empleado = await _db.Empleados
            .FirstOrDefaultAsync(x => x.Id == empleadoId, cancellationToken);

        if (empleado is null)
            return NotFound(new { message = "El empleado no existe." });

        if (!empleado.Activo)
            return BadRequest(new { message = "No se pueden agregar documentos a un empleado inactivo." });

        if (!Enum.IsDefined(typeof(TipoDocumentoEmpleado), request.Tipo))
            return BadRequest(new { message = "El tipo de documento es inválido." });

        if (request.FechaDocumento.HasValue &&
            request.FechaVencimiento.HasValue &&
            request.FechaVencimiento.Value < request.FechaDocumento.Value)
        {
            return BadRequest(new
            {
                message = "La fecha de vencimiento no puede ser menor que la fecha del documento."
            });
        }

        EmpleadoDocumentoStoredFile? storedFile = null;

        try
        {
            storedFile = await _storage.SaveAsync(empleadoId, request.Archivo, cancellationToken);

            var entity = new EmpleadoDocumento
            {
                EmpleadoId = empleadoId,
                Tipo = (TipoDocumentoEmpleado)request.Tipo,
                NombreArchivoOriginal = storedFile.NombreArchivoOriginal,
                NombreArchivoGuardado = storedFile.NombreArchivoGuardado,
                RutaRelativa = storedFile.RutaRelativa,
                MimeType = storedFile.MimeType,
                TamanoBytes = storedFile.TamanoBytes,
                FechaDocumento = request.FechaDocumento,
                FechaVencimiento = request.FechaVencimiento,
                Comentario = string.IsNullOrWhiteSpace(request.Comentario)
                    ? null
                    : request.Comentario.Trim(),
                Activo = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.EmpleadoDocumentos.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            var dto = ToDto(entity);

            return CreatedAtAction(
                nameof(GetByEmpleado),
                new { empleadoId },
                dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch
        {
            if (storedFile is not null)
            {
                _storage.DeleteIfExists(storedFile.RutaRelativa);
            }

            throw;
        }
    }

    [HttpGet("EmpleadoDocumentos/{id:int}/download")]
    public async Task<IActionResult> Download(
        int id,
        CancellationToken cancellationToken)
    {
        var entity = await _db.EmpleadoDocumentos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.Activo, cancellationToken);

        if (entity is null)
            return NotFound(new { message = "El documento no existe." });

        try
        {
            var stream = _storage.OpenRead(entity.RutaRelativa);
            return File(stream, entity.MimeType, entity.NombreArchivoOriginal);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "El archivo físico no fue encontrado." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("EmpleadoDocumentos/{id:int}")]
    public async Task<ActionResult<EmpleadoDocumentoDto>> Update(
        int id,
        [FromBody] EmpleadoDocumentoUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (!Enum.IsDefined(typeof(TipoDocumentoEmpleado), request.Tipo))
            return BadRequest(new { message = "El tipo de documento es inválido." });

        if (request.FechaDocumento.HasValue &&
            request.FechaVencimiento.HasValue &&
            request.FechaVencimiento.Value < request.FechaDocumento.Value)
        {
            return BadRequest(new
            {
                message = "La fecha de vencimiento no puede ser menor que la fecha del documento."
            });
        }

        var entity = await _db.EmpleadoDocumentos
            .FirstOrDefaultAsync(x => x.Id == id && x.Activo, cancellationToken);

        if (entity is null)
            return NotFound(new { message = "El documento no existe." });

        entity.Tipo = (TipoDocumentoEmpleado)request.Tipo;
        entity.FechaDocumento = request.FechaDocumento;
        entity.FechaVencimiento = request.FechaVencimiento;
        entity.Comentario = string.IsNullOrWhiteSpace(request.Comentario)
            ? null
            : request.Comentario.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpDelete("EmpleadoDocumentos/{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var entity = await _db.EmpleadoDocumentos
            .FirstOrDefaultAsync(x => x.Id == id && x.Activo, cancellationToken);

        if (entity is null)
            return NotFound(new { message = "El documento no existe." });

        entity.Activo = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _storage.DeleteIfExists(entity.RutaRelativa);

        return NoContent();
    }

    private static EmpleadoDocumentoDto ToDto(EmpleadoDocumento entity)
    {
        return new EmpleadoDocumentoDto
        {
            Id = entity.Id,
            EmpleadoId = entity.EmpleadoId,
            Tipo = (int)entity.Tipo,
            TipoNombre = entity.Tipo.ToString(),
            NombreArchivoOriginal = entity.NombreArchivoOriginal,
            MimeType = entity.MimeType,
            TamanoBytes = entity.TamanoBytes,
            FechaDocumento = entity.FechaDocumento,
            FechaVencimiento = entity.FechaVencimiento,
            Comentario = entity.Comentario,
            Activo = entity.Activo,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }
}