using Gv.Rh.Application.Interfaces;
using Gv.Rh.Api.Contracts.EmpleadoDocumentos;
using Gv.Rh.Api.Services;
using Gv.Rh.Application.DTOs.EmpleadosDocumentos;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class EmpleadoDocumentosController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly IEmpleadoDocumentoStorageService _storage;

    private static readonly ChecklistDefinition[] ChecklistDefinitions =
    [
        new(TipoDocumentoEmpleado.Ine, true),
        new(TipoDocumentoEmpleado.Curp, true),
        new(TipoDocumentoEmpleado.Rfc, true),
        new(TipoDocumentoEmpleado.Nss, true),
        new(TipoDocumentoEmpleado.ActaNacimiento, true),
        new(TipoDocumentoEmpleado.ComprobanteDomicilio, true),
        new(TipoDocumentoEmpleado.Contrato, true),
        new(TipoDocumentoEmpleado.CartaPolicia, true),
        new(TipoDocumentoEmpleado.CertificadoMedico, false),
        new(TipoDocumentoEmpleado.Otro, false),
    ];
    private readonly IEmpleadoAccessScopeService _empleadoAccessScopeService;


    public EmpleadoDocumentosController(
        RhDbContext db,
        IEmpleadoDocumentoStorageService storage,
        IEmpleadoAccessScopeService empleadoAccessScopeService)
    {
        _db = db;
        _storage = storage;
            _empleadoAccessScopeService = empleadoAccessScopeService;
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

        var accessDenied = await EnsureCanAccessEmpleadoAsync(empleadoId, cancellationToken);
        if (accessDenied is not null)
            return accessDenied;

        var entities = await _db.EmpleadoDocumentos
            .AsNoTracking()
            .Where(x => x.EmpleadoId == empleadoId && x.Activo)
            .OrderBy(x => x.Tipo)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var items = entities
            .Select(ToDto)
            .ToList();

        return Ok(items);
    }

    [HttpGet("Empleados/{empleadoId:int}/documentos/checklist")]
    public async Task<ActionResult<EmpleadoDocumentoChecklistDto>> GetChecklist(
        int empleadoId,
        CancellationToken cancellationToken)
    {
        var empleadoExiste = await _db.Empleados
            .AsNoTracking()
            .AnyAsync(x => x.Id == empleadoId, cancellationToken);

        if (!empleadoExiste)
            return NotFound(new { message = "El empleado no existe." });

        var accessDenied = await EnsureCanAccessEmpleadoAsync(empleadoId, cancellationToken);
        if (accessDenied is not null)
            return accessDenied;

        var documentos = await _db.EmpleadoDocumentos
            .AsNoTracking()
            .Where(x => x.EmpleadoId == empleadoId && x.Activo)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var latestByTipo = documentos
            .GroupBy(x => x.Tipo)
            .ToDictionary(
                g => g.Key,
                g => g.First());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var warningDate = today.AddDays(30);

        var items = ChecklistDefinitions
            .Select(def =>
            {
                latestByTipo.TryGetValue(def.Tipo, out var documento);
                var estatus = ResolveChecklistStatus(documento, def.Requerido, today, warningDate);

                return new EmpleadoDocumentoChecklistItemDto
                {
                    Tipo = (int)def.Tipo,
                    TipoNombre = def.Tipo.ToString(),
                    Requerido = def.Requerido,
                    TieneDocumento = documento is not null,
                    Estatus = estatus,
                    DocumentoId = documento?.Id,
                    NombreArchivoOriginal = documento?.NombreArchivoOriginal,
                    FechaDocumento = documento?.FechaDocumento,
                    FechaVencimiento = documento?.FechaVencimiento
                };
            })
            .OrderByDescending(x => x.Requerido)
            .ThenBy(x => x.Tipo)
            .ToList();

        var requiredItems = items.Where(x => x.Requerido).ToList();

        var totalRequeridos = requiredItems.Count;
        var totalCargados = requiredItems.Count(x => IsChecklistCompliant(x.Estatus));
        var totalFaltantes = requiredItems.Count(x => x.Estatus == "FALTANTE");
        var totalPorVencer = requiredItems.Count(x => x.Estatus == "POR_VENCER");
        var totalVencidos = requiredItems.Count(x => x.Estatus == "VENCIDO");

        var porcentajeCumplimiento = totalRequeridos == 0
            ? 100m
            : Math.Round((decimal)totalCargados / totalRequeridos * 100m, 2);

        var dto = new EmpleadoDocumentoChecklistDto
        {
            EmpleadoId = empleadoId,
            TotalRequeridos = totalRequeridos,
            TotalCargados = totalCargados,
            TotalFaltantes = totalFaltantes,
            TotalPorVencer = totalPorVencer,
            TotalVencidos = totalVencidos,
            PorcentajeCumplimiento = porcentajeCumplimiento,
            Items = items
        };

        return Ok(dto);
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

        var accessDenied = await EnsureCanAccessEmpleadoAsync(empleadoId, cancellationToken);
        if (accessDenied is not null)
            return accessDenied;

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

            return CreatedAtAction(
                nameof(GetByEmpleado),
                new { empleadoId },
                ToDto(entity));
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

        var accessDenied = await EnsureCanAccessEmpleadoAsync(entity.EmpleadoId, cancellationToken);
        if (accessDenied is not null)
            return accessDenied;

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

        if (!CurrentUserHasAnyRole("ADMIN", "RRHH"))
            return Forbid();

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

    [HttpPost("EmpleadoDocumentos/{id:int}/reemplazar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<EmpleadoDocumentoDto>> Replace(
        int id,
        [FromForm] EmpleadoDocumentoReplaceRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

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

        var accessDenied = await EnsureCanAccessEmpleadoAsync(entity.EmpleadoId, cancellationToken);
        if (accessDenied is not null)
            return accessDenied;

        var rutaAnterior = entity.RutaRelativa;

        try
        {
            var storedFile = await _storage.ReplaceAsync(
                entity.EmpleadoId,
                request.Archivo,
                rutaAnterior,
                cancellationToken);

            entity.NombreArchivoOriginal = storedFile.NombreArchivoOriginal;
            entity.NombreArchivoGuardado = storedFile.NombreArchivoGuardado;
            entity.RutaRelativa = storedFile.RutaRelativa;
            entity.MimeType = storedFile.MimeType;
            entity.TamanoBytes = storedFile.TamanoBytes;
            entity.FechaDocumento = request.FechaDocumento;
            entity.FechaVencimiento = request.FechaVencimiento;
            entity.Comentario = string.IsNullOrWhiteSpace(request.Comentario)
                ? entity.Comentario
                : request.Comentario.Trim();
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(ToDto(entity));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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

        if (!CurrentUserHasAnyRole("ADMIN", "RRHH"))
            return Forbid();

        entity.Activo = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _storage.DeleteIfExists(entity.RutaRelativa);

        return NoContent();
    }

    private async Task<ActionResult?> EnsureCanAccessEmpleadoAsync(
        int empleadoId,
        CancellationToken cancellationToken)
    {
        if (_empleadoAccessScopeService.IsAdminOrRrhh(User))
        {
            return null;
        }

        var linkedEmpleadoId = await _empleadoAccessScopeService.ResolveCurrentEmpleadoIdAsync(
            User,
            cancellationToken);

        if (!linkedEmpleadoId.HasValue || linkedEmpleadoId.Value != empleadoId)
        {
            return Forbid();
        }

        return null;
    }

    private bool CurrentUserHasAnyRole(params string[] roles)
    {
        var allowed = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
            return false;

        var currentRoles = User.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role ||
                claim.Type == "role" ||
                claim.Type == "roles" ||
                claim.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(role => role.Trim().ToUpperInvariant())
            .Where(role => !string.IsNullOrWhiteSpace(role));

        return currentRoles.Any(allowed.Contains);
    }

    private async Task<int?> ResolveCurrentEmpleadoIdAsync(CancellationToken cancellationToken)
    {
        var empleadoIdFromClaim = TryGetIntClaimValue(
            "empleadoId",
            "EmpleadoId",
            "empleado_id",
            "rh_empleado_id");

        if (empleadoIdFromClaim.HasValue && empleadoIdFromClaim.Value > 0)
            return empleadoIdFromClaim.Value;

        var userId = TryGetCurrentUserId();
        if (userId.HasValue)
        {
            var empleadoIdByUserId = await _db.Users
                .AsNoTracking()
                .Where(x => x.Id == userId.Value && x.IsActive)
                .Select(x => x.EmpleadoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdByUserId.HasValue && empleadoIdByUserId.Value > 0)
                return empleadoIdByUserId.Value;
        }

        var email = GetCurrentUserEmail();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var empleadoIdByEmail = await _db.Users
                .AsNoTracking()
                .Where(x => x.IsActive && x.Email.ToLower() == normalizedEmail)
                .Select(x => x.EmpleadoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdByEmail.HasValue && empleadoIdByEmail.Value > 0)
                return empleadoIdByEmail.Value;

            var empleadoIdDirectByEmail = await _db.Empleados
                .AsNoTracking()
                .Where(x => x.Email != null && x.Email.ToLower() == normalizedEmail)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdDirectByEmail.HasValue && empleadoIdDirectByEmail.Value > 0)
                return empleadoIdDirectByEmail.Value;
        }

        return null;
    }

    private int? TryGetCurrentUserId()
    {
        return TryGetIntClaimValue(
            ClaimTypes.NameIdentifier,
            "sub",
            ClaimTypes.Sid,
            "nameid",
            "userId",
            "UserId",
            "uid");
    }

    private int? TryGetIntClaimValue(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = User.FindFirstValue(claimType);
            if (int.TryParse(value, out var parsed) && parsed > 0)
                return parsed;
        }

        return null;
    }

    private string? GetCurrentUserEmail()
    {
        var value =
            User.FindFirstValue(ClaimTypes.Email) ??
            User.FindFirstValue("email") ??
            User.FindFirstValue("preferred_username") ??
            User.FindFirstValue("unique_name") ??
            User.FindFirstValue("upn") ??
            User.FindFirstValue(ClaimTypes.Name) ??
            User.Identity?.Name;

        var normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Contains('@')
            ? normalized
            : null;
    }

    private static string ResolveChecklistStatus(
        EmpleadoDocumento? documento,
        bool requerido,
        DateOnly today,
        DateOnly warningDate)
    {
        if (documento is null)
            return requerido ? "FALTANTE" : "OPCIONAL";

        if (!documento.FechaVencimiento.HasValue)
            return "CARGADO";

        var fechaVencimiento = documento.FechaVencimiento.Value;

        if (fechaVencimiento < today)
            return "VENCIDO";

        if (fechaVencimiento <= warningDate)
            return "POR_VENCER";

        return "CARGADO";
    }

    private static bool IsChecklistCompliant(string estatus)
    {
        return estatus is "CARGADO" or "POR_VENCER";
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

    private sealed record ChecklistDefinition(
        TipoDocumentoEmpleado Tipo,
        bool Requerido);
}