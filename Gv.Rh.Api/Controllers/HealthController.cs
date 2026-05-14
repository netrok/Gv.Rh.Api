using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        RhDbContext db,
        IWebHostEnvironment environment,
        ILogger<HealthController> logger)
    {
        _db = db;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("health")]
    [HttpGet("api/health")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var checkedAtUtc = DateTimeOffset.UtcNow;
        var databaseOk = false;
        var databaseStatus = "unknown";

        try
        {
            databaseOk = await _db.Database.CanConnectAsync(cancellationToken);
            databaseStatus = databaseOk ? "ok" : "unavailable";
        }
        catch (Exception ex)
        {
            databaseStatus = "error";

            _logger.LogWarning(
                ex,
                "Health check database validation failed.");
        }

        var finishedAtUtc = DateTimeOffset.UtcNow;
        var durationMs = Math.Round((finishedAtUtc - checkedAtUtc).TotalMilliseconds, 2);

        var response = new
        {
            status = databaseOk ? "ok" : "degraded",
            app = "Gv.Rh.Api",
            environment = _environment.EnvironmentName,
            checkedAtUtc,
            durationMs,
            database = new
            {
                status = databaseStatus,
                provider = "PostgreSQL"
            }
        };

        return StatusCode(databaseOk ? 200 : 503, response);
    }
}
