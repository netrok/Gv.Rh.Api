namespace Gv.Rh.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }

    // Se setea normalmente desde el interceptor; dejamos default por seguridad.
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public int? UserId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }

    // CREATE/UPDATE/DELETE/RESTORE/SOFT_DELETE/etc.
    public string Action { get; set; } = default!;

    // Empleado, AppUser, etc.
    public string Entity { get; set; } = default!;

    // En tu esquema actual es requerido
    public string EntityId { get; set; } = default!;

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }

    // Before/After en JSON (jsonb en PostgreSQL)
    public string? ChangesJson { get; set; }
}