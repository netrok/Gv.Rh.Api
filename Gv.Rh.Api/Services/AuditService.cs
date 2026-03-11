using System.Security.Claims;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

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

    public async Task WriteAsync(
        string action,
        string entityName,
        string recordId,
        string? oldValuesJson = null,
        string? newValuesJson = null,
        string? changedColumnsJson = null)
    {
        var ctx = _http.HttpContext;

        int? userId = null;

        var sub =
            ctx?.User?.FindFirstValue("sub") ??
            ctx?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(sub, out var uid))
            userId = uid;

        var email =
            ctx?.User?.FindFirstValue("email") ??
            ctx?.User?.FindFirstValue(ClaimTypes.Email);

        var role =
            ctx?.User?.FindFirstValue(ClaimTypes.Role) ??
            ctx?.User?.Claims.FirstOrDefault(c => c.Type == "role")?.Value;

        var ip = ctx?.Connection?.RemoteIpAddress?.ToString();

        var userAgent = ctx?.Request?.Headers["User-Agent"].ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
            userAgent = null;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            UserId = userId,
            UserEmail = email,
            UserRole = role,
            Action = action,
            EntityName = entityName,
            RecordId = recordId,
            IpAddress = ip,
            UserAgent = userAgent,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            ChangedColumnsJson = changedColumnsJson
        });

        await _db.SaveChangesAsync();
    }
}