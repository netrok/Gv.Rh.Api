using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class EmpleadoMovimientoLaboral
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    public TipoMovimientoLaboral TipoMovimiento { get; set; }

    public DateOnly FechaMovimiento { get; set; }

    public TipoBajaEmpleado? TipoBaja { get; set; }

    public string? Motivo { get; set; }

    public string? Comentario { get; set; }

    public bool? Recontratable { get; set; }

    public int? UsuarioResponsableId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}