using Gv.Rh.Application.DTOs.Reclutamiento.Reportes;
using Gv.Rh.Application.Interfaces.Reclutamiento;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public class ReclutamientoController : ControllerBase
{
    private readonly IReclutamientoReporteService _reporteService;

    public ReclutamientoController(IReclutamientoReporteService reporteService)
    {
        _reporteService = reporteService;
    }

    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] ReclutamientoReporteFiltroDto filtro,
        CancellationToken cancellationToken)
    {
        var content = await _reporteService.ExportarXlsxAsync(filtro, cancellationToken);
        var fileName = $"reclutamiento_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] ReclutamientoReporteFiltroDto filtro,
        CancellationToken cancellationToken)
    {
        var content = await _reporteService.ExportarPdfAsync(filtro, cancellationToken);
        var fileName = $"reclutamiento_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

        return File(content, "application/pdf", fileName);
    }
}