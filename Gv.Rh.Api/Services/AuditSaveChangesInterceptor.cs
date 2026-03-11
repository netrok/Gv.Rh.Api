using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Gv.Rh.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
        string? UserEmail,
        string? UserRole,
        string? IpAddress,
        string? UserAgent,
        string Action,
        string EntityName,
        EntityEntry Entry,
        string? RecordIdBefore,
        string? OldValuesJson,
        string? NewValuesJson,
        string? ChangedColumnsJson
    );

    private sealed record ChangeParts(
        string? OldValuesJson,
        string? NewValuesJson,
        string? ChangedColumnsJson
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

        // Conservador: si no hay usuario autenticado, no auditamos.
        // Evita ruido en seed/startup/jobs.
        if (httpCtx?.User?.Identity?.IsAuthenticated != true) return;

        var entries = db.ChangeTracker.Entries()
            .Where(e =>
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                e.Entity is not AuditLog &&
                e.Entity is not RefreshToken)
            .ToList();

        if (entries.Count == 0) return;

        var now = DateTime.UtcNow;

        var userId =
            TryGetIntClaim(httpCtx.User, JwtRegisteredClaimNames.Sub) ??
            TryGetIntClaim(httpCtx.User, ClaimTypes.NameIdentifier);

        var email =
            httpCtx.User.FindFirstValue(JwtRegisteredClaimNames.Email) ??
            httpCtx.User.FindFirstValue(ClaimTypes.Email);

        var role =
            httpCtx.User.FindFirstValue(ClaimTypes.Role) ??
            httpCtx.User.Claims.FirstOrDefault(c => c.Type == "role")?.Value;

        var ip = httpCtx.Connection.RemoteIpAddress?.ToString();

        var userAgent = httpCtx.Request.Headers["User-Agent"].ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
            userAgent = null;

        _drafts.Clear();

        foreach (var entry in entries)
        {
            var entityName = entry.Metadata.ClrType.Name;

            var action = GetAction(entry);
            var changes = BuildChangeParts(entry);
            if (changes is null) continue;

            // Para DELETE conviene capturar la PK antes
            var recordIdBefore = entry.State == EntityState.Deleted
                ? GetPrimaryKeyString(entry, useOriginalValues: true)
                : null;

            _drafts.Add(new AuditDraft(
                OccurredAtUtc: now,
                UserId: userId,
                UserEmail: email,
                UserRole: role,
                IpAddress: ip,
                UserAgent: userAgent,
                Action: action,
                EntityName: entityName,
                Entry: entry,
                RecordIdBefore: recordIdBefore,
                OldValuesJson: changes.OldValuesJson,
                NewValuesJson: changes.NewValuesJson,
                ChangedColumnsJson: changes.ChangedColumnsJson
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
                var recordId = d.RecordIdBefore ?? GetPrimaryKeyString(d.Entry, useOriginalValues: false);

                if (string.IsNullOrWhiteSpace(recordId))
                    continue;

                logs.Add(new AuditLog
                {
                    OccurredAtUtc = d.OccurredAtUtc,
                    UserId = d.UserId,
                    UserEmail = d.UserEmail,
                    UserRole = d.UserRole,
                    Action = d.Action,
                    EntityName = d.EntityName,
                    RecordId = recordId,
                    IpAddress = d.IpAddress,
                    UserAgent = d.UserAgent,
                    OldValuesJson = d.OldValuesJson,
                    NewValuesJson = d.NewValuesJson,
                    ChangedColumnsJson = d.ChangedColumnsJson
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

    private static string GetAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
            return "CREATE";

        if (entry.State == EntityState.Deleted)
            return "DELETE";

        if (entry.State == EntityState.Modified)
        {
            // Soft delete por IsDeleted
            var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
            if (isDeletedProp is not null && isDeletedProp.IsModified)
            {
                var before = isDeletedProp.OriginalValue is bool ob && ob;
                var after = isDeletedProp.CurrentValue is bool cb && cb;

                if (!before && after) return "SOFT_DELETE";
                if (before && !after) return "RESTORE";
            }

            // Soft delete por DeletedAtUtc
            var deletedAtProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAtUtc");
            if (deletedAtProp is not null && deletedAtProp.IsModified)
            {
                var before = deletedAtProp.OriginalValue;
                var after = deletedAtProp.CurrentValue;

                if (before is null && after is not null) return "SOFT_DELETE";
                if (before is not null && after is null) return "RESTORE";
            }

            // Caso típico de tu proyecto: Activo
            var activoProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Activo");
            if (activoProp is not null && activoProp.IsModified)
            {
                var before = Convert.ToBoolean(activoProp.OriginalValue);
                var after = Convert.ToBoolean(activoProp.CurrentValue);

                if (before && !after) return "SOFT_DELETE";
                if (!before && after) return "RESTORE";
            }

            return "UPDATE";
        }

        return "UPDATE";
    }

    private static ChangeParts? BuildChangeParts(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            var newValues = new Dictionary<string, object?>();
            var changedColumns = new List<string>();

            foreach (var p in entry.Properties)
            {
                if (p.Metadata.IsPrimaryKey()) continue;
                if (p.Metadata.IsShadowProperty()) continue;

                newValues[p.Metadata.Name] = p.CurrentValue;
                changedColumns.Add(p.Metadata.Name);
            }

            return new ChangeParts(
                OldValuesJson: null,
                NewValuesJson: JsonSerializer.Serialize(newValues, JsonOpts),
                ChangedColumnsJson: JsonSerializer.Serialize(changedColumns, JsonOpts)
            );
        }

        if (entry.State == EntityState.Deleted)
        {
            var oldValues = new Dictionary<string, object?>();
            var changedColumns = new List<string>();

            foreach (var p in entry.Properties)
            {
                if (p.Metadata.IsPrimaryKey()) continue;
                if (p.Metadata.IsShadowProperty()) continue;

                oldValues[p.Metadata.Name] = p.OriginalValue;
                changedColumns.Add(p.Metadata.Name);
            }

            return new ChangeParts(
                OldValuesJson: JsonSerializer.Serialize(oldValues, JsonOpts),
                NewValuesJson: null,
                ChangedColumnsJson: JsonSerializer.Serialize(changedColumns, JsonOpts)
            );
        }

        if (entry.State == EntityState.Modified)
        {
            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();
            var changedColumns = new List<string>();

            foreach (var p in entry.Properties)
            {
                if (!p.IsModified) continue;
                if (p.Metadata.IsPrimaryKey()) continue;
                if (p.Metadata.IsShadowProperty()) continue;

                oldValues[p.Metadata.Name] = p.OriginalValue;
                newValues[p.Metadata.Name] = p.CurrentValue;
                changedColumns.Add(p.Metadata.Name);
            }

            if (changedColumns.Count == 0)
                return null;

            return new ChangeParts(
                OldValuesJson: JsonSerializer.Serialize(oldValues, JsonOpts),
                NewValuesJson: JsonSerializer.Serialize(newValues, JsonOpts),
                ChangedColumnsJson: JsonSerializer.Serialize(changedColumns, JsonOpts)
            );
        }

        return null;
    }

    private static string? GetPrimaryKeyString(EntityEntry entry, bool useOriginalValues)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null) return null;

        var parts = pk.Properties
            .Select(p =>
            {
                var propertyEntry = entry.Property(p.Name);
                var value = useOriginalValues ? propertyEntry.OriginalValue : propertyEntry.CurrentValue;
                return value?.ToString() ?? "";
            })
            .ToArray();

        var joined = string.Join("|", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}