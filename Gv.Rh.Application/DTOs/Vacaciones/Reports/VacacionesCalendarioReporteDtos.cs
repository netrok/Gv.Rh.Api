namespace Gv.Rh.Application.DTOs.Vacaciones.Reports;

public sealed class VacacionesCalendarioReporteQueryDto
{
    public int? EmpleadoId { get; set; }
    public int? SucursalId { get; set; }
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }

    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }

    public bool? SoloActivos { get; set; }
    public string? Search { get; set; }
}

public sealed class VacacionesCalendarioReporteResultDto
{
    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }

    public int TotalSolicitudes { get; set; }
    public int TotalEmpleados { get; set; }
    public decimal TotalDias { get; set; }

    public string SucursalPrincipal { get; set; } = "—";

    public List<VacacionesCalendarioReporteRowDto> Items { get; set; } = [];
}

public sealed class VacacionesCalendarioReporteRowDto
{
    public int SolicitudId { get; set; }

    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;

    public string? Sucursal { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }

    public decimal DiasSolicitados { get; set; }

    public string Estatus { get; set; } = string.Empty;

    public int? AprobadorEmpleadoId { get; set; }
    public string? AprobadorEmpleado { get; set; }

    public DateTime? FechaResolucionUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
