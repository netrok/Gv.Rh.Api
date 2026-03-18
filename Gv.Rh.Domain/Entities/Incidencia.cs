using Gv.Rh.Domain.Common.Enums;

namespace Gv.Rh.Domain.Entities;

public class Incidencia
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    public int? SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    public TipoIncidencia Tipo { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }

    public string? Comentario { get; set; }

    public EstatusIncidencia Estatus { get; set; } = EstatusIncidencia.PENDIENTE;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}