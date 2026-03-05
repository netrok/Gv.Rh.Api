namespace Gv.Rh.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public int? UserId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }

    public string Action { get; set; } = default!;  // CREATE/UPDATE/DELETE/RESTORE
    public string Entity { get; set; } = default!;  // Empleado
    public string EntityId { get; set; } = default!;

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }

    // ✅ Before/After en JSON (jsonb en PostgreSQL)
    public string? ChangesJson { get; set; }
}