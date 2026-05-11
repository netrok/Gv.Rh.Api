using Gv.Rh.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN,RRHH")]
[Route("api/Aprobaciones")]
[Produces("application/json")]
public sealed class AprobacionesController : ControllerBase
{
    private readonly IAprobacionesNotificationService _notificationService;

    public AprobacionesController(
        IAprobacionesNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("notificar-pendientes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> NotificarPendientes(
        CancellationToken cancellationToken)
    {
        var creadas = await _notificationService.NotificarResumenPendientesAsync(
            cancellationToken);

        return Ok(new
        {
            message = "Resumen de aprobaciones pendientes procesado correctamente.",
            notificacionesCreadas = creadas
        });
    }
}
