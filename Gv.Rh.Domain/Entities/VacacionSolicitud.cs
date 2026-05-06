using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class VacacionSolicitud
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public int? VacacionPeriodoId { get; set; }
    public VacacionPeriodo? VacacionPeriodo { get; set; }

    public int? VacacionMovimientoId { get; set; }
    public VacacionMovimiento? VacacionMovimiento { get; set; }

    public int? IncidenciaId { get; set; }
    public Incidencia? Incidencia { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }

    public decimal DiasSolicitados { get; set; }

    public EstatusVacacionSolicitud Estatus { get; set; } =
        EstatusVacacionSolicitud.PENDIENTE;

    public string? ComentarioEmpleado { get; set; }
    public string? ComentarioResolucion { get; set; }
    public string? MotivoRechazo { get; set; }

    public int? SolicitadaPorUsuarioId { get; set; }
    public AppUser? SolicitadaPorUsuario { get; set; }

    public int? ResueltaPorUsuarioId { get; set; }
    public AppUser? ResueltaPorUsuario { get; set; }

    public int? AprobadorEmpleadoId { get; set; }
    public Empleado? AprobadorEmpleado { get; set; }

    public DateTime? FechaResolucionUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}