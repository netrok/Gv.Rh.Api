using Gv.Rh.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;

namespace Gv.Rh.Api.Services;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _http;

    // Evita auditar el SaveChanges extra que hacemos al final (sin recursión)
    private static readonly AsyncLocal<bool> _suppressAudit = new();

    // Guardamos pares (auditLog, empleado) SOLO para CREATE, para corregir EntityId con el Id real
    private static readonly ConditionalWeakTable<DbContext, List<(AuditLog log, Empleado emp)>> _pendingCreateFix = new();

    public AuditSaveChangesInterceptor(IHttpContextAccessor http) => _http = http;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FixCreateEntityIds(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await FixCreateEntityIds(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditLogs(DbContext? db)
    {
        if (db is null) return;
        if (_suppressAudit.Value) return;

        var entries = db.ChangeTracker.Entries()
            .Where(e =>
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                e.Entity is not AuditLog)
            .ToList();

        if (entries.Count == 0) return;

        var ctx = _http.HttpContext;

        int? userId = null;
        var sub = ctx?.User?.FindFirstValue("sub");
        if (int.TryParse(sub, out var uid)) userId = uid;

        var email = ctx?.User?.FindFirstValue("email") ?? ctx?.User?.FindFirstValue(ClaimTypes.Email);
        var role = ctx?.User?.FindFirstValue(ClaimTypes.Role);
        var ip = ctx?.Connection?.RemoteIpAddress?.ToString();
        var ua = ctx?.Request?.Headers.UserAgent.ToString();

        foreach (var e in entries)
        {
            // Por ahora solo auditamos Empleado
            if (e.Entity is not Empleado emp) continue;

            var action = e.State switch
            {
                EntityState.Added => "CREATE",
                EntityState.Deleted => "DELETE",
                EntityState.Modified => "UPDATE",
                _ => "UNKNOWN"
            };

            // Si es Modified, detecta baja/restauración por Activo
            if (e.State == EntityState.Modified)
            {
                var activoProp = e.Properties.FirstOrDefault(p => p.Metadata.Name == "Activo");
                if (activoProp != null && activoProp.IsModified)
                {
                    var beforeActivo = Convert.ToBoolean(activoProp.OriginalValue);
                    var afterActivo = Convert.ToBoolean(activoProp.CurrentValue);

                    if (beforeActivo && !afterActivo) action = "DELETE";
                    else if (!beforeActivo && afterActivo) action = "RESTORE";
                }
            }

            // Para CREATE aún no tenemos Id final: usamos NumEmpleado TEMP
            var entityId = e.State == EntityState.Added
                ? emp.NumEmpleado
                : emp.Id.ToString();

            var changesJson = BuildChangesJson(e);

            var log = new AuditLog
            {
                OccurredAtUtc = DateTime.UtcNow,
                UserId = userId,
                Email = email,
                Role = role,
                Action = action,
                Entity = "Empleado",
                EntityId = entityId,
                Ip = ip,
                UserAgent = string.IsNullOrWhiteSpace(ua) ? null : ua,
                ChangesJson = changesJson
            };

            db.Set<AuditLog>().Add(log);

            // Guardar para corregir EntityId cuando ya exista Id real
            if (e.State == EntityState.Added)
            {
                var list = _pendingCreateFix.GetOrCreateValue(db);
                list.Add((log, emp));
            }
        }
    }

    private async Task FixCreateEntityIds(DbContext? db)
    {
        if (db is null) return;
        if (_suppressAudit.Value) return;

        if (!_pendingCreateFix.TryGetValue(db, out var list) || list.Count == 0)
            return;

        foreach (var (log, emp) in list)
        {
            if (emp.Id > 0)
                log.EntityId = emp.Id.ToString();
        }

        list.Clear();

        _suppressAudit.Value = true;
        try
        {
            await db.SaveChangesAsync();
        }
        finally
        {
            _suppressAudit.Value = false;
        }
    }

    private static string? BuildChangesJson(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry e)
    {
        var before = new Dictionary<string, object?>();
        var after = new Dictionary<string, object?>();

        foreach (var p in e.Properties)
        {
            if (p.Metadata.IsShadowProperty()) continue;

            if (e.State == EntityState.Added)
            {
                after[p.Metadata.Name] = p.CurrentValue;
            }
            else if (e.State == EntityState.Deleted)
            {
                before[p.Metadata.Name] = p.OriginalValue;
            }
            else if (e.State == EntityState.Modified && p.IsModified)
            {
                before[p.Metadata.Name] = p.OriginalValue;
                after[p.Metadata.Name] = p.CurrentValue;
            }
        }

        if (before.Count == 0 && after.Count == 0) return null;

        var payload = new
        {
            before = before.Count == 0 ? null : before,
            after = after.Count == 0 ? null : after
        };

        return JsonSerializer.Serialize(payload);
    }
}