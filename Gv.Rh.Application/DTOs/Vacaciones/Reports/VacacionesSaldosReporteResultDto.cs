namespace Gv.Rh.Application.DTOs.Vacaciones.Reports;

public sealed class VacacionesSaldosReporteResultDto
{
    public DateOnly FechaCorte { get; set; }

    public int TotalRegistros { get; set; }
    public int EmpleadosConSaldo { get; set; }
    public int PeriodosVencidos { get; set; }

    public decimal TotalDiasDerecho { get; set; }
    public decimal TotalDiasTomados { get; set; }
    public decimal TotalDiasPagados { get; set; }
    public decimal TotalDiasVencidos { get; set; }
    public decimal TotalSaldo { get; set; }

    public List<VacacionesSaldosReporteRowDto> Items { get; set; } = [];
}