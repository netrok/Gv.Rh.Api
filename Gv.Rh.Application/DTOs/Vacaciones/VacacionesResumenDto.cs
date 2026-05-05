namespace Gv.Rh.Application.DTOs.Vacaciones;

public class VacacionesResumenDto
{
    public int EmpleadoId { get; set; }

    public string NumEmpleado { get; set; } = string.Empty;

    public string EmpleadoNombre { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de ingreso original registrada en el expediente del empleado.
    /// Se conserva para historial general de RH.
    /// </summary>
    public DateOnly FechaIngresoOriginal { get; set; }

    /// <summary>
    /// Fecha base usada para calcular vacaciones del ciclo laboral actual.
    /// En ciclo 1 normalmente coincide con FechaIngresoOriginal.
    /// En reingreso corresponde a la fecha del último REINGRESO.
    /// </summary>
    public DateOnly FechaIngreso { get; set; }

    /// <summary>
    /// Ciclo laboral vigente para vacaciones.
    /// Alta inicial = 1. Cada reingreso incrementa el ciclo.
    /// </summary>
    public int CicloLaboralActual { get; set; } = 1;

    /// <summary>
    /// Fecha desde la que se calcula la antigüedad vacacional del ciclo actual.
    /// </summary>
    public DateOnly FechaBaseCicloLaboral { get; set; }

    public int AntiguedadAnios { get; set; }

    public DateOnly? ProximoAniversario { get; set; }

    public int? PoliticaId { get; set; }

    public string? PoliticaNombre { get; set; }

    public decimal PrimaVacacionalPorcentaje { get; set; }

    public decimal DiasDerechoTotal { get; set; }

    public decimal DiasTomadosTotal { get; set; }

    public decimal DiasPagadosTotal { get; set; }

    public decimal DiasAjustadosTotal { get; set; }

    public decimal DiasVencidosTotal { get; set; }

    public decimal SaldoDisponible { get; set; }

    public int PeriodosTotales { get; set; }

    public int PeriodosAbiertos { get; set; }

    public VacacionPeriodoDto? PeriodoActual { get; set; }
}