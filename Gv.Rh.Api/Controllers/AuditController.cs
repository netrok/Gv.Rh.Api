using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "ADMIN,RRHH")]
public class AuditController : ControllerBase
{
    private readonly RhDbContext _db;

    public AuditController(RhDbContext db) => _db = db;

    // GET /api/audit?entity=Empleado&entityId=123&action=UPDATE&email=rrhh@rh.local&from=2026-03-01&to=2026-03-04&page=1&pageSize=50
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? entity = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? email = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entity))
            q = q.Where(x => x.Entity == entity.Trim());

        if (!string.IsNullOrWhiteSpace(entityId))
            q = q.Where(x => x.EntityId == entityId.Trim());

        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(x => x.Action == action.Trim());

        if (!string.IsNullOrWhiteSpace(email))
        {
            var term = email.Trim();
            q = q.Where(x => x.Email != null && EF.Functions.ILike(x.Email, $"%{term}%"));
        }

        if (from.HasValue)
            q = q.Where(x => x.OccurredAtUtc >= from.Value.ToUniversalTime());

        if (to.HasValue)
            q = q.Where(x => x.OccurredAtUtc <= to.Value.ToUniversalTime());

        q = q.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id);

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            items
        });
    }
}