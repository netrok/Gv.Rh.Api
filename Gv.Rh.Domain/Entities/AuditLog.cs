namespace Gv.Rh.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }

    // Fecha real del evento en UTC
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    // Usuario que ejecutó la acción
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }

    // CREATE, UPDATE, SOFT_DELETE, RESTORE, DELETE, etc.
    public string Action { get; set; } = default!;

    // Empleado, Departamento, Puesto, AppUser, etc.
    public string EntityName { get; set; } = default!;

    // Se deja string para soportar cualquier PK
    public string RecordId { get; set; } = default!;

    // Contexto técnico útil
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // JSONs separados para auditoría seria
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? ChangedColumnsJson { get; set; }
}