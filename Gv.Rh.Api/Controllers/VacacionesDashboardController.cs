using Gv.Rh.Application.DTOs.Vacaciones.Dashboard;
using Gv.Rh.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN,RRHH")]
[Route("api/Vacaciones/dashboard")]
[Produces("application/json")]
public sealed class VacacionesDashboardController : ControllerBase
{
    private readonly IVacacionesDashboardService _vacacionesDashboardService;

    public VacacionesDashboardController(
        IVacacionesDashboardService vacacionesDashboardService)
    {
        _vacacionesDashboardService = vacacionesDashboardService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(VacacionesDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VacacionesDashboardDto>> Get(
        CancellationToken cancellationToken)
    {
        var result = await _vacacionesDashboardService.GetDashboardAsync(
            cancellationToken);

        return Ok(result);
    }
}
