using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace Gv.Rh.Api.Services;

public sealed class AuditLogger
{
    private readonly RhDbContext _db;
    private readonly IHttpContextAccessor _http;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public AuditLogger(RhDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAuthAsync(
        string action,
        AppUser? user = null,
        object? data = null,
        CancellationToken ct = default)
    {
        var ctx = _http.HttpContext;
        if (ctx is null) return; // startup/seed -> no audit

        // Actor: si viene user (login/refresh), úsalo; si no, intenta claims (logout/logout-all)
        var (uid, email, role) = GetActor(ctx.User, user);

        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        var ua = ctx.Request.Headers.UserAgent.ToString();

        // Entity/EntityId para agrupar auth
        var entity = "Auth";
        var entityId = user is not null ? user.Id.ToString() : (uid?.ToString() ?? (email ?? "unknown"));

        var log = new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            UserId = uid,
            Email = email,
            Role = role,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Ip = ip,
            UserAgent = string.IsNullOrWhiteSpace(ua) ? null : ua,
            ChangesJson = data is null ? null : JsonSerializer.Serialize(data, JsonOpts)
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    private static (int? userId, string? email, string? role) GetActor(ClaimsPrincipal principal, AppUser? user)
    {
        if (user is not null)
            return (user.Id, user.Email, user.Role);

        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        int? uid = int.TryParse(sub, out var x) ? x : null;

        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email)
                   ?? principal.FindFirstValue(ClaimTypes.Email);

        var role = principal.FindFirstValue(ClaimTypes.Role);

        return (uid, email, role);
    }
}