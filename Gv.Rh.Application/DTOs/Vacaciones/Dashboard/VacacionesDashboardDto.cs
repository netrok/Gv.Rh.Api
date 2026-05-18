namespace Gv.Rh.Application.DTOs.Vacaciones.Dashboard;

public sealed class VacacionesDashboardDto
{
    public DateOnly FechaCorte { get; set; }

    public int EmpleadosActivos { get; set; }
    public int EmpleadosConSaldo { get; set; }
    public int EmpleadosSinPeriodoAbierto { get; set; }

    public int PeriodosAbiertos { get; set; }
    public int PeriodosVencidos { get; set; }
    public int PeriodosPorVencer30Dias { get; set; }

    public decimal SaldoTotal { get; set; }
    public decimal DiasDerechoTotal { get; set; }
    public decimal DiasTomadosTotal { get; set; }
    public decimal DiasVencidosTotal { get; set; }

    public int MovimientosMes { get; set; }
    public int MovimientosImportacionLegacy { get; set; }

    public int SolicitudesPendientes { get; set; }
    public int SolicitudesAprobadasMes { get; set; }
    public int SolicitudesRechazadasMes { get; set; }
    public int SolicitudesCanceladasMes { get; set; }

    public decimal DiasSolicitadosPendientes { get; set; }
    public decimal DiasAprobadosMes { get; set; }

    public List<VacacionesDashboardSaldoAltoDto> TopSaldos { get; set; } = [];
    public List<VacacionesDashboardPeriodoVencerDto> PeriodosPorVencer { get; set; } = [];
    public List<VacacionesDashboardMovimientoDto> UltimosMovimientos { get; set; } = [];
    public List<VacacionesDashboardAniversarioDto> ProximosAniversarios { get; set; } = [];

    public List<VacacionesDashboardSolicitudDto> SolicitudesPendientesDetalle { get; set; } = [];
    public List<VacacionesDashboardSolicitudDto> ProximasVacacionesAprobadas { get; set; } = [];
}

public sealed class VacacionesDashboardSaldoAltoDto
{
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;

    public string? Sucursal { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }

    public int CicloLaboral { get; set; }
    public int PeriodosAbiertos { get; set; }
    public decimal Saldo { get; set; }
}

public sealed class VacacionesDashboardPeriodoVencerDto
{
    public int VacacionPeriodoId { get; set; }
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;

    public string? Sucursal { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }

    public int CicloLaboral { get; set; }
    public int AnioServicio { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public DateOnly FechaLimiteDisfrute { get; set; }

    public int DiasParaVencer { get; set; }
    public decimal Saldo { get; set; }
}

public sealed class VacacionesDashboardMovimientoDto
{
    public int MovimientoId { get; set; }
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;

    public int VacacionPeriodoId { get; set; }
    public int CicloLaboral { get; set; }

    public string TipoMovimiento { get; set; } = string.Empty;
    public DateOnly FechaMovimiento { get; set; }

    public decimal Dias { get; set; }
    public decimal SaldoAntes { get; set; }
    public decimal SaldoDespues { get; set; }

    public string? Referencia { get; set; }
    public string? Origen { get; set; }
    public string? Comentario { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class VacacionesDashboardAniversarioDto
{
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;

    public string? Sucursal { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }

    public DateOnly FechaBaseCicloLaboral { get; set; }
    public DateOnly ProximoAniversario { get; set; }
    public int DiasRestantes { get; set; }
    public int AniosServicioCumplidos { get; set; }
}

public sealed class VacacionesDashboardSolicitudDto
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
    public string EstatusNombre { get; set; } = string.Empty;

    public string? ComentarioEmpleado { get; set; }

    public int? AprobadorEmpleadoId { get; set; }
    public string? AprobadorEmpleado { get; set; }

    public DateTime? FechaResolucionUtc { get; set; }

    public int DiasParaInicio { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}