using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Gv.Rh.Api.Services;

public class AuditService
{
    private readonly RhDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(RhDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task WriteAsync(string action, string entity, string entityId)
    {
        var ctx = _http.HttpContext;

        int? userId = null;
        var sub = ctx?.User?.FindFirstValue("sub");
        if (int.TryParse(sub, out var uid)) userId = uid;

        var email = ctx?.User?.FindFirstValue("email") ?? ctx?.User?.FindFirstValue(ClaimTypes.Email);
        var role = ctx?.User?.FindFirstValue(ClaimTypes.Role);

        var ip = ctx?.Connection?.RemoteIpAddress?.ToString();
        var ua = ctx?.Request?.Headers.UserAgent.ToString();

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            UserId = userId,
            Email = email,
            Role = role,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Ip = ip,
            UserAgent = string.IsNullOrWhiteSpace(ua) ? null : ua
        });

        await _db.SaveChangesAsync();
    }
}