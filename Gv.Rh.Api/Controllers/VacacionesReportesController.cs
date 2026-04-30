using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Vacaciones.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN,RRHH")]
[Route("api/Vacaciones/reportes")]
public sealed class VacacionesReportesController : ControllerBase
{
    private readonly IVacacionesReportService _vacacionesReportService;

    public VacacionesReportesController(
        IVacacionesReportService vacacionesReportService)
    {
        _vacacionesReportService = vacacionesReportService;
    }

    [HttpGet("saldos")]
    [ProducesResponseType(typeof(VacacionesSaldosReporteResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesSaldosReporteResultDto>> GetSaldos(
        [FromQuery] VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await _vacacionesReportService.GetSaldosAsync(
            query,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("saldos/export/xlsx")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportSaldosXlsx(
        [FromQuery] VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var file = await _vacacionesReportService.BuildSaldosXlsxAsync(
            query,
            cancellationToken);

        return File(
            file.Content,
            file.ContentType,
            file.FileName);
    }

    [HttpGet("saldos/export/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportSaldosPdf(
        [FromQuery] VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var file = await _vacacionesReportService.BuildSaldosPdfAsync(
            query,
            cancellationToken);

        return File(
            file.Content,
            file.ContentType,
            file.FileName);
    }
}