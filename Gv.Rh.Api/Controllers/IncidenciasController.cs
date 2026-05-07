using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Incidencias;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common.Enums;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncidenciasController : ControllerBase
{
    private readonly RhDbContext _context;
    private readonly IIncidenciasReportService _incidenciasReportService;
    private readonly IIncidenciaAuthorizationService _incidenciaAuthorizationService;
    private readonly IEmpleadoAccessScopeService _empleadoAccessScopeService;

    public sealed record ResolverIncidenciaDto(string? Comentario);

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public IncidenciasController(
        RhDbContext context,
        IIncidenciasReportService incidenciasReportService,
        IIncidenciaAuthorizationService incidenciaAuthorizationService,
        IEmpleadoAccessScopeService empleadoAccessScopeService)
    {
        _context = context;
        _incidenciasReportService = incidenciasReportService;
        _incidenciaAuthorizationService = incidenciaAuthorizationService;
        _empleadoAccessScopeService = empleadoAccessScopeService;
    }

    [HttpGet]
    [Authorize(Roles = "ADMIN,RRHH,JEFE,EMPLEADO")]
    public async Task<ActionResult<IEnumerable<IncidenciaDto>>> GetAll(
        [FromQuery] IncidenciaQueryDto query,
        CancellationToken cancellationToken)
    {
        var items = await ApplyAccessScope(BuildIncidenciasBaseQuery(query))
            .OrderByDescending(x => x.FechaInicio)
            .ThenByDescending(x => x.Id)
            .Select(x => new IncidenciaDto
            {
                Id = x.Id,
                EmpleadoId = x.EmpleadoId,
                EmpleadoNombre = x.Empleado.Nombres + " " + x.Empleado.ApellidoPaterno +
                                 (!string.IsNullOrWhiteSpace(x.Empleado.ApellidoMaterno)
                                     ? " " + x.Empleado.ApellidoMaterno
                                     : ""),
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Tipo = x.Tipo,
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Comentario = x.Comentario,
                Estatus = x.Estatus,
                TieneEvidencia = x.TieneEvidencia,
                EvidenciaNombreOriginal = x.EvidenciaNombreOriginal,
                EvidenciaContentType = x.EvidenciaContentType,
                EvidenciaTamanoBytes = x.EvidenciaTamanoBytes,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("export/xlsx")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] IncidenciaQueryDto query,
        CancellationToken cancellationToken)
    {
        var file = await _incidenciasReportService.BuildXlsxAsync(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("export/pdf")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] IncidenciaQueryDto query,
        CancellationToken cancellationToken)
    {
        var file = await _incidenciasReportService.BuildPdfAsync(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "ADMIN,RRHH,JEFE,EMPLEADO")]
    public async Task<ActionResult<IncidenciaDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var canView = await _incidenciaAuthorizationService.CanViewAsync(User, id, cancellationToken);
        if (!canView)
        {
            return Forbid();
        }

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
                                 (!string.IsNullOrWhiteSpace(x.Empleado.ApellidoMaterno)
                                     ? " " + x.Empleado.ApellidoMaterno
                                     : ""),
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Tipo = x.Tipo,
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Comentario = x.Comentario,
                Estatus = x.Estatus,
                TieneEvidencia = x.TieneEvidencia,
                EvidenciaNombreOriginal = x.EvidenciaNombreOriginal,
                EvidenciaContentType = x.EvidenciaContentType,
                EvidenciaTamanoBytes = x.EvidenciaTamanoBytes,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,RRHH,JEFE")]
    public async Task<ActionResult<IncidenciaDto>> Create(
        [FromBody] CreateIncidenciaDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.FechaFin < dto.FechaInicio)
            return BadRequest(new { message = "La fecha fin no puede ser menor que la fecha inicio." });

        var empleado = await _context.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.EmpleadoId, cancellationToken);

        if (empleado is null)
            return BadRequest(new { message = "El empleado no existe." });

        if (!empleado.Activo)
            return BadRequest(new { message = "No se puede crear una incidencia para un empleado inactivo." });

        int? sucursalId = dto.SucursalId ?? empleado.SucursalId;

        if (sucursalId.HasValue)
        {
            var sucursalExiste = await _context.Sucursales
                .AsNoTracking()
                .AnyAsync(x => x.Id == sucursalId.Value, cancellationToken);

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
        await _context.SaveChangesAsync(cancellationToken);

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
                                 (!string.IsNullOrWhiteSpace(x.Empleado.ApellidoMaterno)
                                     ? " " + x.Empleado.ApellidoMaterno
                                     : ""),
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Tipo = x.Tipo,
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Comentario = x.Comentario,
                Estatus = x.Estatus,
                TieneEvidencia = x.TieneEvidencia,
                EvidenciaNombreOriginal = x.EvidenciaNombreOriginal,
                EvidenciaContentType = x.EvidenciaContentType,
                EvidenciaTamanoBytes = x.EvidenciaTamanoBytes,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<ActionResult<IncidenciaDto>> Update(
        int id,
        [FromBody] UpdateIncidenciaDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.FechaFin < dto.FechaInicio)
            return BadRequest(new { message = "La fecha fin no puede ser menor que la fecha inicio." });

        var entity = await _context.Incidencias.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        if (entity.Estatus != EstatusIncidencia.PENDIENTE)
            return BadRequest(new { message = "Solo se pueden editar incidencias en estatus PENDIENTE." });

        var empleado = await _context.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.EmpleadoId, cancellationToken);

        if (empleado is null)
            return BadRequest(new { message = "El empleado no existe." });

        if (!empleado.Activo)
            return BadRequest(new { message = "No se puede asignar una incidencia a un empleado inactivo." });

        int? sucursalId = dto.SucursalId ?? empleado.SucursalId;

        if (sucursalId.HasValue)
        {
            var sucursalExiste = await _context.Sucursales
                .AsNoTracking()
                .AnyAsync(x => x.Id == sucursalId.Value, cancellationToken);

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

        await _context.SaveChangesAsync(cancellationToken);

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
                                 (!string.IsNullOrWhiteSpace(x.Empleado.ApellidoMaterno)
                                     ? " " + x.Empleado.ApellidoMaterno
                                     : ""),
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Tipo = x.Tipo,
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                Comentario = x.Comentario,
                Estatus = x.Estatus,
                TieneEvidencia = x.TieneEvidencia,
                EvidenciaNombreOriginal = x.EvidenciaNombreOriginal,
                EvidenciaContentType = x.EvidenciaContentType,
                EvidenciaTamanoBytes = x.EvidenciaTamanoBytes,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstAsync(cancellationToken);

        return Ok(updated);
    }

    [HttpPost("{id:int}/aprobar")]
    [Authorize(Roles = "ADMIN,RRHH,JEFE")]
    public async Task<IActionResult> Aprobar(
        int id,
        [FromBody] ResolverIncidenciaDto? dto,
        CancellationToken cancellationToken)
    {
        var entity = await _context.Incidencias.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        var canResolve = await _incidenciaAuthorizationService.CanResolveAsync(User, id, cancellationToken);
        if (!canResolve)
        {
            return Forbid();
        }

        if (entity.Estatus != EstatusIncidencia.PENDIENTE)
            return BadRequest(new { message = "Solo se pueden aprobar incidencias en estatus PENDIENTE." });

        entity.Estatus = EstatusIncidencia.APROBADA;
        entity.ResueltaPorUsuarioId = _empleadoAccessScopeService.GetCurrentUserId(User);
        entity.ResueltaPorEmpleadoId = _empleadoAccessScopeService.GetCurrentEmpleadoIdFromClaims(User);
        entity.FechaResolucionUtc = DateTime.UtcNow;
        entity.ComentarioResolucion = NormalizeNullable(dto?.Comentario);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Incidencia aprobada correctamente." });
    }

    [HttpPost("{id:int}/rechazar")]
    [Authorize(Roles = "ADMIN,RRHH,JEFE")]
    public async Task<IActionResult> Rechazar(
        int id,
        [FromBody] ResolverIncidenciaDto? dto,
        CancellationToken cancellationToken)
    {
        var entity = await _context.Incidencias.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        var canResolve = await _incidenciaAuthorizationService.CanResolveAsync(User, id, cancellationToken);
        if (!canResolve)
        {
            return Forbid();
        }

        if (entity.Estatus != EstatusIncidencia.PENDIENTE)
            return BadRequest(new { message = "Solo se pueden rechazar incidencias en estatus PENDIENTE." });

        entity.Estatus = EstatusIncidencia.RECHAZADA;
        entity.ResueltaPorUsuarioId = _empleadoAccessScopeService.GetCurrentUserId(User);
        entity.ResueltaPorEmpleadoId = _empleadoAccessScopeService.GetCurrentEmpleadoIdFromClaims(User);
        entity.FechaResolucionUtc = DateTime.UtcNow;
        entity.ComentarioResolucion = NormalizeNullable(dto?.Comentario);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Incidencia rechazada correctamente." });
    }

    [HttpPost("{id:int}/evidencia")]
    [Authorize(Roles = "ADMIN,RRHH")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType(typeof(IncidenciaEvidenciaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IncidenciaEvidenciaDto>> UploadEvidencia(
        int id,
        [FromForm] IFormFile archivo,
        CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { message = "Debes seleccionar un archivo." });

        if (archivo.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "El archivo excede el tamaño máximo permitido de 5 MB." });

        var extension = Path.GetExtension(archivo.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            return BadRequest(new { message = "Tipo de archivo no permitido." });

        if (!AllowedContentTypes.Contains(archivo.ContentType))
            return BadRequest(new { message = "Content-Type no permitido." });

        var incidencia = await _context.Incidencias.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (incidencia is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        var uploadsRoot = Path.Combine(AppContext.BaseDirectory, "storage", "incidencias");
        var folder = Path.Combine(
            uploadsRoot,
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"));

        Directory.CreateDirectory(folder);

        if (!string.IsNullOrWhiteSpace(incidencia.EvidenciaRuta))
        {
            var oldFullPath = Path.Combine(AppContext.BaseDirectory, incidencia.EvidenciaRuta);
            if (System.IO.File.Exists(oldFullPath))
                System.IO.File.Delete(oldFullPath);
        }

        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, safeFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await archivo.CopyToAsync(stream, cancellationToken);
        }

        var relativePath = Path.GetRelativePath(AppContext.BaseDirectory, fullPath);

        incidencia.TieneEvidencia = true;
        incidencia.EvidenciaNombreOriginal = Path.GetFileName(archivo.FileName);
        incidencia.EvidenciaNombreArchivo = safeFileName;
        incidencia.EvidenciaContentType = archivo.ContentType;
        incidencia.EvidenciaTamanoBytes = archivo.Length;
        incidencia.EvidenciaRuta = relativePath.Replace("\\", "/");
        incidencia.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new IncidenciaEvidenciaDto
        {
            TieneEvidencia = incidencia.TieneEvidencia,
            EvidenciaNombreOriginal = incidencia.EvidenciaNombreOriginal,
            EvidenciaContentType = incidencia.EvidenciaContentType,
            EvidenciaTamanoBytes = incidencia.EvidenciaTamanoBytes
        });
    }

    [HttpGet("{id:int}/evidencia")]
    public async Task<IActionResult> DownloadEvidencia(int id, CancellationToken cancellationToken)
    {
        var canView = await _incidenciaAuthorizationService.CanViewAsync(User, id, cancellationToken);
        if (!canView)
        {
            return Forbid();
        }

        var incidencia = await _context.Incidencias
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (incidencia is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        if (!incidencia.TieneEvidencia || string.IsNullOrWhiteSpace(incidencia.EvidenciaRuta))
            return NotFound(new { message = "La incidencia no tiene evidencia." });

        var fullPath = Path.Combine(AppContext.BaseDirectory, incidencia.EvidenciaRuta);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "El archivo de evidencia no existe en disco." });

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fullPath, out var contentType))
            contentType = incidencia.EvidenciaContentType ?? "application/octet-stream";

        var fileName = incidencia.EvidenciaNombreOriginal ?? Path.GetFileName(fullPath);
        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);

        return File(bytes, contentType, fileName);
    }

    [HttpDelete("{id:int}/evidencia")]
    [Authorize(Roles = "ADMIN,RRHH")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteEvidencia(int id, CancellationToken cancellationToken)
    {
        var incidencia = await _context.Incidencias.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (incidencia is null)
            return NotFound(new { message = "Incidencia no encontrada." });

        if (!string.IsNullOrWhiteSpace(incidencia.EvidenciaRuta))
        {
            var fullPath = Path.Combine(AppContext.BaseDirectory, incidencia.EvidenciaRuta);
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }

        incidencia.TieneEvidencia = false;
        incidencia.EvidenciaNombreOriginal = null;
        incidencia.EvidenciaNombreArchivo = null;
        incidencia.EvidenciaContentType = null;
        incidencia.EvidenciaTamanoBytes = null;
        incidencia.EvidenciaRuta = null;
        incidencia.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
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

    private IQueryable<Incidencia> BuildIncidenciasBaseQuery(IncidenciaQueryDto query)
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

        if (query.SoloPendientes)
            incidenciasQuery = incidenciasQuery.Where(x => x.Estatus == EstatusIncidencia.PENDIENTE);

        if (query.FechaDesde.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.FechaInicio >= query.FechaDesde.Value);

        if (query.FechaHasta.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.FechaFin <= query.FechaHasta.Value);

        return incidenciasQuery;
    }

    private IQueryable<Incidencia> ApplyAccessScope(IQueryable<Incidencia> query)
    {
        return _empleadoAccessScopeService.ApplyIncidenciaScope(query, User);
    }

    private static string? NormalizeNullable(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}