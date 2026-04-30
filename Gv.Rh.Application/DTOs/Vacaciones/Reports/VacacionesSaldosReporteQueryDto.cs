namespace Gv.Rh.Application.DTOs.Vacaciones.Reports;

public sealed class VacacionesSaldosReporteQueryDto
{
    public int? SucursalId { get; set; }
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }
    public int? EmpleadoId { get; set; }

    public string? EstatusLaboral { get; set; }

    public DateOnly? FechaCorte { get; set; }

    public bool? SoloConSaldo { get; set; }
    public bool? SoloVencidos { get; set; }
    public bool? SoloActivos { get; set; }

    public string? Search { get; set; }
}