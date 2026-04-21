using Gv.Rh.Application.DTOs.EmpleadosDocumentos;
using Gv.Rh.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public sealed class ExpedienteNotificacionesController : ControllerBase
{
    private readonly IExpedienteNotificationService _service;

    public ExpedienteNotificacionesController(IExpedienteNotificationService service)
    {
        _service = service;
    }

    [HttpGet("preview")]
    [ProducesResponseType(typeof(DocumentoVencimientoResumenDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DocumentoVencimientoResumenDto>> Preview(
        [FromQuery] int diasAnticipacion = 30,
        [FromQuery] bool incluirVencidos = true,
        CancellationToken cancellationToken = default)
    {
        diasAnticipacion = NormalizarDiasAnticipacion(diasAnticipacion);

        var result = await _service.ObtenerResumenAsync(
            diasAnticipacion,
            incluirVencidos,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("notificar-vencimientos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> NotificarVencimientos(
        [FromBody] NotificarDocumentosPorVencerRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        var diasAnticipacion = NormalizarDiasAnticipacion(
            request?.DiasAnticipacion ?? 30);

        var incluirVencidos = request?.IncluirVencidos ?? true;

        var enviados = await _service.EnviarResumenAsync(
            diasAnticipacion,
            incluirVencidos,
            cancellationToken);

        return Ok(new
        {
            ok = true,
            enviados,
            diasAnticipacion,
            incluirVencidos,
            mensaje = enviados > 0
                ? $"Se enviaron notificaciones para {enviados} documento(s)."
                : "No se encontraron documentos por vencer o vencidos para notificar."
        });
    }

    private static int NormalizarDiasAnticipacion(int diasAnticipacion)
    {
        if (diasAnticipacion < 1)
            return 1;

        if (diasAnticipacion > 365)
            return 365;

        return diasAnticipacion;
    }
}