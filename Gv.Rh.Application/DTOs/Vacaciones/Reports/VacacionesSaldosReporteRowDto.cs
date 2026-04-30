namespace Gv.Rh.Application.DTOs.Vacaciones.Reports;

public sealed class VacacionesSaldosReporteRowDto
{
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;

    public string? Sucursal { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }

    public string EstatusLaboral { get; set; } = string.Empty;
    public bool Activo { get; set; }

    public DateOnly FechaIngreso { get; set; }

    public int VacacionPeriodoId { get; set; }
    public int AnioServicio { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public DateOnly FechaLimiteDisfrute { get; set; }

    public decimal DiasDerecho { get; set; }
    public decimal DiasTomados { get; set; }
    public decimal DiasPagados { get; set; }
    public decimal DiasAjustados { get; set; }
    public decimal DiasVencidos { get; set; }
    public decimal Saldo { get; set; }

    public string EstatusPeriodo { get; set; } = string.Empty;
    public bool PrimaPagada { get; set; }

    public bool EstaVencido { get; set; }
    public int DiasParaVencer { get; set; }
}