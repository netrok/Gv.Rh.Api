using Gv.Rh.Application.DTOs.Reclutamiento.Candidatos;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public class CandidatosController : ControllerBase
{
    private readonly RhDbContext _db;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private const long MaxCvSizeBytes = 8 * 1024 * 1024;

    public CandidatosController(RhDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<CandidatoListItemDto>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] string? fuenteReclutamiento,
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Candidatos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                x.Nombres.Contains(q) ||
                x.ApellidoPaterno.Contains(q) ||
                (x.ApellidoMaterno != null && x.ApellidoMaterno.Contains(q)) ||
                (x.Email != null && x.Email.Contains(q)) ||
                (x.Telefono != null && x.Telefono.Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(fuenteReclutamiento))
        {
            fuenteReclutamiento = fuenteReclutamiento.Trim();
            query = query.Where(x => x.FuenteReclutamiento == fuenteReclutamiento);
        }

        if (soloActivos)
            query = query.Where(x => x.Activo);

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CandidatoListItemDto
            {
                Id = x.Id,
                NombreCompleto = (x.Nombres + " " + x.ApellidoPaterno + " " + (x.ApellidoMaterno ?? "")).Trim(),
                Telefono = x.Telefono,
                Email = x.Email,
                FuenteReclutamiento = x.FuenteReclutamiento,
                PretensionSalarial = x.PretensionSalarial,
                TieneCv = x.CvRutaRelativa != null,
                Activo = x.Activo,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CandidatoDetailDto>> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.Candidatos
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CandidatoDetailDto
            {
                Id = x.Id,
                Nombres = x.Nombres,
                ApellidoPaterno = x.ApellidoPaterno,
                ApellidoMaterno = x.ApellidoMaterno,
                NombreCompleto = (x.Nombres + " " + x.ApellidoPaterno + " " + (x.ApellidoMaterno ?? "")).Trim(),
                Telefono = x.Telefono,
                Email = x.Email,
                FechaNacimiento = x.FechaNacimiento,
                Ciudad = x.Ciudad,
                FuenteReclutamiento = x.FuenteReclutamiento,
                ResumenPerfil = x.ResumenPerfil,
                PretensionSalarial = x.PretensionSalarial,
                TieneCv = x.CvRutaRelativa != null,
                CvNombreOriginal = x.CvNombreOriginal,
                CvMimeType = x.CvMimeType,
                CvTamanoBytes = x.CvTamanoBytes,
                Activo = x.Activo,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound("Candidato no encontrado.");

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult> Create(
        [FromBody] CreateCandidatoRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = new Candidato
        {
            Nombres = request.Nombres.Trim(),
            ApellidoPaterno = request.ApellidoPaterno.Trim(),
            ApellidoMaterno = string.IsNullOrWhiteSpace(request.ApellidoMaterno) ? null : request.ApellidoMaterno.Trim(),
            Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            FechaNacimiento = request.FechaNacimiento,
            Ciudad = string.IsNullOrWhiteSpace(request.Ciudad) ? null : request.Ciudad.Trim(),
            FuenteReclutamiento = string.IsNullOrWhiteSpace(request.FuenteReclutamiento) ? null : request.FuenteReclutamiento.Trim(),
            ResumenPerfil = string.IsNullOrWhiteSpace(request.ResumenPerfil) ? null : request.ResumenPerfil.Trim(),
            PretensionSalarial = request.PretensionSalarial,
            Activo = request.Activo,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Candidatos.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { entity.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(
        int id,
        [FromBody] UpdateCandidatoRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Candidatos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Candidato no encontrado.");

        entity.Nombres = request.Nombres.Trim();
        entity.ApellidoPaterno = request.ApellidoPaterno.Trim();
        entity.ApellidoMaterno = string.IsNullOrWhiteSpace(request.ApellidoMaterno) ? null : request.ApellidoMaterno.Trim();
        entity.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();
        entity.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        entity.FechaNacimiento = request.FechaNacimiento;
        entity.Ciudad = string.IsNullOrWhiteSpace(request.Ciudad) ? null : request.Ciudad.Trim();
        entity.FuenteReclutamiento = string.IsNullOrWhiteSpace(request.FuenteReclutamiento) ? null : request.FuenteReclutamiento.Trim();
        entity.ResumenPerfil = string.IsNullOrWhiteSpace(request.ResumenPerfil) ? null : request.ResumenPerfil.Trim();
        entity.PretensionSalarial = request.PretensionSalarial;
        entity.Activo = request.Activo;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/cv")]
    [RequestSizeLimit(MaxCvSizeBytes)]
    public async Task<ActionResult> UploadCv(
        int id,
        IFormFile archivo,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Candidatos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Candidato no encontrado.");

        if (archivo is null || archivo.Length <= 0)
            return BadRequest("Debes adjuntar un archivo.");

        if (archivo.Length > MaxCvSizeBytes)
            return BadRequest("El CV excede el tamaño máximo permitido de 8 MB.");

        var extension = Path.GetExtension(archivo.FileName);
        if (!AllowedExtensions.Contains(extension))
            return BadRequest("Solo se permiten archivos PDF, DOC o DOCX.");

        if (!AllowedContentTypes.Contains(archivo.ContentType))
            return BadRequest("Tipo de contenido no permitido para CV.");

        if (!string.IsNullOrWhiteSpace(entity.CvRutaRelativa))
        {
            var oldFullPath = Path.Combine(AppContext.BaseDirectory, entity.CvRutaRelativa);
            if (System.IO.File.Exists(oldFullPath))
                System.IO.File.Delete(oldFullPath);
        }

        var folderRelative = Path.Combine("storage", "candidatos-cv", DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var folderFull = Path.Combine(AppContext.BaseDirectory, folderRelative);
        Directory.CreateDirectory(folderFull);

        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folderFull, safeFileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await archivo.CopyToAsync(stream, cancellationToken);
        }

        entity.CvNombreOriginal = archivo.FileName;
        entity.CvNombreGuardado = safeFileName;
        entity.CvRutaRelativa = Path.Combine(folderRelative, safeFileName);
        entity.CvMimeType = archivo.ContentType;
        entity.CvTamanoBytes = archivo.Length;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "CV cargado correctamente." });
    }

    [HttpGet("{id:int}/cv")]
    public async Task<ActionResult> DownloadCv(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Candidatos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Candidato no encontrado.");

        if (string.IsNullOrWhiteSpace(entity.CvRutaRelativa))
            return NotFound("El candidato no tiene CV cargado.");

        var fullPath = Path.Combine(AppContext.BaseDirectory, entity.CvRutaRelativa);
        if (!System.IO.File.Exists(fullPath))
            return NotFound("No se encontró el archivo físico del CV.");

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(entity.CvNombreOriginal ?? fullPath, out var contentType))
            contentType = entity.CvMimeType ?? "application/octet-stream";

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);
        return File(bytes, contentType, entity.CvNombreOriginal ?? "cv");
    }

    [HttpDelete("{id:int}/cv")]
    public async Task<ActionResult> DeleteCv(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Candidatos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Candidato no encontrado.");

        if (string.IsNullOrWhiteSpace(entity.CvRutaRelativa))
            return BadRequest("El candidato no tiene CV cargado.");

        var fullPath = Path.Combine(AppContext.BaseDirectory, entity.CvRutaRelativa);
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        entity.CvNombreOriginal = null;
        entity.CvNombreGuardado = null;
        entity.CvRutaRelativa = null;
        entity.CvMimeType = null;
        entity.CvTamanoBytes = null;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}