using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Vacaciones.Solicitudes;

public sealed class VacacionesSolicitudDto
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;

    public string? Sucursal { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }

    public int? VacacionPeriodoId { get; set; }
    public int? VacacionMovimientoId { get; set; }
    public int? IncidenciaId { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public decimal DiasSolicitados { get; set; }

    public EstatusVacacionSolicitud Estatus { get; set; }
    public string EstatusNombre { get; set; } = string.Empty;

    public string? ComentarioEmpleado { get; set; }
    public string? ComentarioResolucion { get; set; }
    public string? MotivoRechazo { get; set; }

    public int? SolicitadaPorUsuarioId { get; set; }
    public string? SolicitadaPorUsuario { get; set; }

    public int? ResueltaPorUsuarioId { get; set; }
    public string? ResueltaPorUsuario { get; set; }

    public int? AprobadorEmpleadoId { get; set; }
    public string? AprobadorEmpleado { get; set; }

    public DateTime? FechaResolucionUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class VacacionesSolicitudQueryDto
{
    public int? EmpleadoId { get; set; }
    public int? SucursalId { get; set; }
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }

    public EstatusVacacionSolicitud? Estatus { get; set; }

    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }

    public bool? SoloPendientes { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class VacacionesSolicitudListResultDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }

    public int Pendientes { get; set; }
    public int Aprobadas { get; set; }
    public int Rechazadas { get; set; }
    public int Canceladas { get; set; }

    public List<VacacionesSolicitudDto> Items { get; set; } = [];
}

public sealed class VacacionesSolicitudCreateDto
{
    public int? EmpleadoId { get; set; }
    public int? VacacionPeriodoId { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }

    public decimal DiasSolicitados { get; set; }

    public string? ComentarioEmpleado { get; set; }
}

public sealed class VacacionesSolicitudResolverDto
{
    public string? ComentarioResolucion { get; set; }
    public string? MotivoRechazo { get; set; }
}

public sealed class VacacionesSolicitudActorDto
{
    public int? UsuarioId { get; set; }
    public string? UsuarioEmail { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? EmpleadoId { get; set; }
}