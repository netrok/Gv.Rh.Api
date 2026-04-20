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
    private readonly IExpedienteNotificationService _expedienteNotificationService;

    public ExpedienteNotificacionesController(
        IExpedienteNotificationService expedienteNotificationService)
    {
        _expedienteNotificationService = expedienteNotificationService;
    }

    [HttpPost("documentos-por-vencer")]
    public async Task<IActionResult> NotificarDocumentosPorVencer(
        [FromBody] NotificarDocumentosPorVencerRequestDto? request,
        CancellationToken cancellationToken)
    {
        var diasAnticipacion = request?.DiasAnticipacion ?? 15;

        if (diasAnticipacion < 1)
        {
            diasAnticipacion = 1;
        }

        if (diasAnticipacion > 365)
        {
            diasAnticipacion = 365;
        }

        var total = await _expedienteNotificationService
            .NotificarDocumentosPorVencerAsync(diasAnticipacion, cancellationToken);

        return Ok(new
        {
            enviados = total,
            diasAnticipacion,
            mensaje = total > 0
                ? $"Se notificaron {total} documento(s) próximos a vencer."
                : "No se encontraron documentos por vencer en el rango solicitado."
        });
    }
}