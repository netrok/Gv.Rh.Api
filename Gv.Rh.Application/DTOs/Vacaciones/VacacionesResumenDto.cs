namespace Gv.Rh.Application.DTOs.Vacaciones;

public class VacacionesResumenDto
{
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string EmpleadoNombre { get; set; } = string.Empty;
    public DateOnly FechaIngreso { get; set; }
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
