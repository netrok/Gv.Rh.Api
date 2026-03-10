using Gv.Rh.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace Gv.Rh.Infrastructure.Persistence;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _http;

    // Evita recursión cuando guardamos los audit logs en un segundo SaveChanges
    private bool _suppress;

    private readonly List<AuditDraft> _drafts = new();

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private sealed record AuditDraft(
        DateTime OccurredAtUtc,
        int? UserId,
        string? Email,
        string? Role,
        string? Ip,
        string? UserAgent,
        string Action,
        string Entity,
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry,
        string? EntityIdBefore,
        string? ChangesJson
    );

    public AuditSaveChangesInterceptor(IHttpContextAccessor http)
    {
        _http = http;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureDrafts(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureDrafts(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PersistLogsAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await PersistLogsAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _drafts.Clear();
        base.SaveChangesFailed(eventData);
    }

    private void CaptureDrafts(DbContext? db)
    {
        if (db is null) return;
        if (_suppress) return;

        var httpCtx = _http.HttpContext;

        // ✅ Conservador: si NO hay usuario autenticado (seed/startup/jobs), NO auditamos.
        if (httpCtx?.User?.Identity?.IsAuthenticated != true) return;

        var entries = db.ChangeTracker.Entries()
            .Where(e =>
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                e.Entity is not AuditLog &&
                e.Entity is not RefreshToken) // no auditar refresh tokens (muchísimo ruido)
            .ToList();

        if (entries.Count == 0) return;

        var now = DateTime.UtcNow;

        var userId = TryGetIntClaim(httpCtx.User, JwtRegisteredClaimNames.Sub)
                     ?? TryGetIntClaim(httpCtx.User, ClaimTypes.NameIdentifier);

        var email = httpCtx.User.FindFirstValue(JwtRegisteredClaimNames.Email)
                    ?? httpCtx.User.FindFirstValue(ClaimTypes.Email);

        var role = httpCtx.User.FindFirstValue(ClaimTypes.Role);

        var ip = httpCtx.Connection.RemoteIpAddress?.ToString();
        var ua = httpCtx.Request.Headers.UserAgent.ToString();

        _drafts.Clear();

        foreach (var entry in entries)
        {
            var entity = entry.Metadata.ClrType.Name;

            // ⚠️ Si quieres whitelist (ej: solo Empleado), descomenta:
            // if (entity != nameof(Empleado)) continue;

            var action = GetAction(entry);
            var changesJson = BuildChangesJson(entry);
            if (changesJson is null) continue;

            // Para DELETE conviene capturar PK antes (después puede quedar Detached)
            var entityIdBefore = entry.State == EntityState.Deleted
                ? GetPrimaryKeyString(entry)
                : null;

            _drafts.Add(new AuditDraft(
                OccurredAtUtc: now,
                UserId: userId,
                Email: email,
                Role: role,
                Ip: ip,
                UserAgent: string.IsNullOrWhiteSpace(ua) ? null : ua,
                Action: action,
                Entity: entity,
                Entry: entry,
                EntityIdBefore: entityIdBefore,
                ChangesJson: changesJson
            ));
        }
    }

    private async Task PersistLogsAsync(DbContext? db, CancellationToken ct)
    {
        if (db is null) return;
        if (_suppress) return;
        if (_drafts.Count == 0) return;

        try
        {
            _suppress = true;

            var logs = new List<AuditLog>(_drafts.Count);

            foreach (var d in _drafts)
            {
                // ✅ En CREATE, aquí ya existe el Id real (identity ya asignado)
                var entityId = d.EntityIdBefore ?? GetPrimaryKeyString(d.Entry);

                // Tu modelo/mapping exige EntityId (string) => si no hay PK, no guardamos basura
                if (string.IsNullOrWhiteSpace(entityId))
                    continue;

                logs.Add(new AuditLog
                {
                    OccurredAtUtc = d.OccurredAtUtc,
                    UserId = d.UserId,
                    Email = d.Email,
                    Role = d.Role,
                    Action = d.Action,
                    Entity = d.Entity,
                    EntityId = entityId,
                    Ip = d.Ip,
                    UserAgent = d.UserAgent,
                    ChangesJson = d.ChangesJson
                });
            }

            if (logs.Count > 0)
            {
                db.Set<AuditLog>().AddRange(logs);
                await db.SaveChangesAsync(ct);
            }
        }
        finally
        {
            _drafts.Clear();
            _suppress = false;
        }
    }

    private static int? TryGetIntClaim(ClaimsPrincipal user, string claimType)
    {
        var raw = user.FindFirstValue(claimType);
        return int.TryParse(raw, out var id) ? id : null;
    }

    private static string GetAction(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.State == EntityState.Added) return "CREATE";
        if (entry.State == EntityState.Deleted) return "DELETE";

        if (entry.State == EntityState.Modified)
        {
            // Soft delete por IsDeleted
            var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
            if (isDeletedProp is not null
                && isDeletedProp.OriginalValue is bool ob
                && isDeletedProp.CurrentValue is bool cb
                && ob == false && cb == true)
                return "SOFT_DELETE";

            // Soft delete por DeletedAtUtc
            var deletedAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAtUtc");
            if (deletedAtProp is not null
                && deletedAtProp.OriginalValue is null
                && deletedAtProp.CurrentValue is not null)
                return "SOFT_DELETE";

            // Caso típico Empleado: Activo false/true
            var activoProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Activo");
            if (activoProp is not null && activoProp.IsModified)
            {
                var before = Convert.ToBoolean(activoProp.OriginalValue);
                var after = Convert.ToBoolean(activoProp.CurrentValue);
                if (before && !after) return "DELETE";
                if (!before && after) return "RESTORE";
            }

            return "UPDATE";
        }

        return "UPDATE";
    }

    private static string? BuildChangesJson(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            var after = new Dictionary<string, object?>();
            foreach (var p in entry.Properties)
            {
                if (p.Metadata.IsPrimaryKey()) continue;
                if (p.Metadata.IsShadowProperty()) continue;
                after[p.Metadata.Name] = p.CurrentValue;
            }
            return JsonSerializer.Serialize(new { after }, JsonOpts);
        }

        if (entry.State == EntityState.Deleted)
        {
            var before = new Dictionary<string, object?>();
            foreach (var p in entry.Properties)
            {
                if (p.Metadata.IsPrimaryKey()) continue;
                if (p.Metadata.IsShadowProperty()) continue;
                before[p.Metadata.Name] = p.OriginalValue;
            }
            return JsonSerializer.Serialize(new { before }, JsonOpts);
        }

        if (entry.State == EntityState.Modified)
        {
            var before = new Dictionary<string, object?>();
            var after = new Dictionary<string, object?>();

            foreach (var p in entry.Properties)
            {
                if (!p.IsModified) continue;
                if (p.Metadata.IsPrimaryKey()) continue;
                if (p.Metadata.IsShadowProperty()) continue;

                before[p.Metadata.Name] = p.OriginalValue;
                after[p.Metadata.Name] = p.CurrentValue;
            }

            if (before.Count == 0) return null;
            return JsonSerializer.Serialize(new { before, after }, JsonOpts);
        }

        return null;
    }

    private static string? GetPrimaryKeyString(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null) return null;

        var parts = pk.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "")
            .ToArray();

        var joined = string.Join("|", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}