using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Empleados;

public class EmpleadoMovimientoLaboralDto
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }

    public TipoMovimientoLaboral TipoMovimiento { get; set; }

    public DateOnly FechaMovimiento { get; set; }

    public TipoBajaEmpleado? TipoBaja { get; set; }

    public string? Motivo { get; set; }

    public string? Comentario { get; set; }

    public bool? Recontratable { get; set; }

    public int? UsuarioResponsableId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}