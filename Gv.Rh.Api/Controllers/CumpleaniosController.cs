using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Cumpleanios;
using Gv.Rh.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public sealed class CumpleaniosController : ControllerBase
{
    private readonly ICumpleaniosService _cumpleaniosService;
    private readonly ICumpleaniosNotificationService _cumpleaniosNotificationService;
    private readonly ICumpleaniosReportService _cumpleaniosReportService;

    public CumpleaniosController(
        ICumpleaniosService cumpleaniosService,
        ICumpleaniosNotificationService cumpleaniosNotificationService,
        ICumpleaniosReportService cumpleaniosReportService)
    {
        _cumpleaniosService = cumpleaniosService;
        _cumpleaniosNotificationService = cumpleaniosNotificationService;
        _cumpleaniosReportService = cumpleaniosReportService;
    }

    [HttpGet("hoy")]
    public async Task<IActionResult> GetHoy(
        [FromQuery] int? sucursalId,
        [FromQuery] int? departamentoId,
        CancellationToken cancellationToken)
    {
        var result = await _cumpleaniosService.GetHoyAsync(
            sucursalId,
            departamentoId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("proximos")]
    public async Task<IActionResult> GetProximos(
        [FromQuery] int dias = 30,
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? departamentoId = null,
        CancellationToken cancellationToken = default)
    {
        if (dias < 1)
        {
            dias = 1;
        }

        if (dias > 90)
        {
            dias = 90;
        }

        var result = await _cumpleaniosService.GetProximosAsync(
            dias,
            sucursalId,
            departamentoId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("mes")]
    public async Task<IActionResult> GetMes(
        [FromQuery] int? anio,
        [FromQuery] int? mes,
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? departamentoId = null,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var finalYear = anio ?? today.Year;
        var finalMonth = mes ?? today.Month;

        if (finalMonth < 1 || finalMonth > 12)
        {
            return BadRequest("El mes debe estar entre 1 y 12.");
        }

        var result = await _cumpleaniosService.GetMesAsync(
            finalYear,
            finalMonth,
            sucursalId,
            departamentoId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen(
        [FromQuery] int? sucursalId,
        [FromQuery] int? departamentoId,
        CancellationToken cancellationToken)
    {
        var result = await _cumpleaniosService.GetResumenAsync(
            sucursalId,
            departamentoId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] CumpleaniosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _cumpleaniosReportService.BuildXlsxAsync(
            query,
            cancellationToken);

        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] CumpleaniosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _cumpleaniosReportService.BuildPdfAsync(
            query,
            cancellationToken);

        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpPost("notificar-hoy")]
    public async Task<IActionResult> NotificarHoy(
        [FromBody] EnviarCumpleaniosHoyRequestDto? request,
        CancellationToken cancellationToken)
    {
        var incluirEdad = request?.IncluirEdad ?? true;

        var total = await _cumpleaniosNotificationService.EnviarCumpleaniosHoyAsync(
            incluirEdad,
            cancellationToken);

        return Ok(new
        {
            enviados = total,
            mensaje = total > 0
                ? $"Se envió la notificación con {total} cumpleaños."
                : "Hoy no hay cumpleaños para notificar."
        });
    }
}