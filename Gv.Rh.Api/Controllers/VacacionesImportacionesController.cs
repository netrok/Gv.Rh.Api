using Gv.Rh.Application.DTOs.Vacaciones.Import;
using Gv.Rh.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN,RRHH")]
[Route("api/Vacaciones/importaciones")]
public class VacacionesImportacionesController : ControllerBase
{
    private readonly IVacacionesLegacyImportService _legacyImportService;

    public VacacionesImportacionesController(
        IVacacionesLegacyImportService legacyImportService)
    {
        _legacyImportService = legacyImportService;
    }

    [HttpPost("legacy-excel/preview")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VacacionesLegacyImportPreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesLegacyImportPreviewDto>> PreviewLegacyExcel(
        [FromForm] VacacionesLegacyExcelPreviewForm request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateExcelFile(request.Archivo);
        if (validation is not null)
            return validation;

        await using var stream = request.Archivo.OpenReadStream();

        var result = await _legacyImportService.PreviewAsync(
            stream,
            request.Archivo.FileName,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("legacy-excel/confirmar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VacacionesLegacyImportConfirmResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesLegacyImportConfirmResultDto>> ConfirmarLegacyExcel(
        [FromForm] VacacionesLegacyExcelConfirmForm request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateExcelFile(request.Archivo);
        if (validation is not null)
            return validation;

        var empleadoIds = ParseEmpleadoIds(request.EmpleadoIds);

        if (!request.ImportarTodosElegibles && empleadoIds.Count == 0)
        {
            return BadRequest(new
            {
                message = "Debes indicar EmpleadoIds separados por coma o activar ImportarTodosElegibles."
            });
        }

        var confirmRequest = new VacacionesLegacyImportConfirmRequestDto
        {
            EmpleadoIdsConfirmados = empleadoIds,
            ImportarTodosElegibles = request.ImportarTodosElegibles,
            PermitirSaldosNegativos = request.PermitirSaldosNegativos,
            Comentario = string.IsNullOrWhiteSpace(request.Comentario)
                ? null
                : request.Comentario.Trim()
        };

        await using var stream = request.Archivo.OpenReadStream();

        var result = await _legacyImportService.ConfirmAsync(
            stream,
            request.Archivo.FileName,
            confirmRequest,
            ResolveCurrentUserId(),
            cancellationToken);

        return Ok(result);
    }

    private BadRequestObjectResult? ValidateExcelFile(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return BadRequest(new
            {
                message = "Debes seleccionar un archivo Excel."
            });
        }

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

        if (extension is not ".xls" and not ".xlsx")
        {
            return BadRequest(new
            {
                message = "Solo se permiten archivos Excel .xls o .xlsx."
            });
        }

        return null;
    }

    private static List<int> ParseEmpleadoIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<int>();

        return raw
            .Split(
                new[] { ',', ';', '|', ' ', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private int? ResolveCurrentUserId()
    {
        var raw =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("nameid") ??
            User.FindFirstValue("userId") ??
            User.FindFirstValue("UserId");

        return int.TryParse(raw, out var id) && id > 0
            ? id
            : null;
    }
}

public sealed class VacacionesLegacyExcelPreviewForm
{
    [Required]
    public IFormFile Archivo { get; set; } = default!;
}

public sealed class VacacionesLegacyExcelConfirmForm
{
    [Required]
    public IFormFile Archivo { get; set; } = default!;

    /// <summary>
    /// IDs de empleados separados por coma, punto y coma, espacio o salto de línea.
    /// Ejemplo: 2,6,8,19.
    /// </summary>
    public string? EmpleadoIds { get; set; }

    /// <summary>
    /// Si es true, importa todos los registros elegibles del preview.
    /// </summary>
    public bool ImportarTodosElegibles { get; set; }

    /// <summary>
    /// Permite importar saldos negativos si nóminas los confirma.
    /// Por defecto se bloquean.
    /// </summary>
    public bool PermitirSaldosNegativos { get; set; }

    public string? Comentario { get; set; }
}
