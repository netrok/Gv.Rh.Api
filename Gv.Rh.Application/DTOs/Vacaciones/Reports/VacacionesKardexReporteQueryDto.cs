namespace Gv.Rh.Application.DTOs.Vacaciones.Reports;

public sealed class VacacionesKardexReporteQueryDto
{
    public int? SucursalId { get; set; }
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }
    public int? EmpleadoId { get; set; }

    public string? EstatusLaboral { get; set; }
    public string? TipoMovimiento { get; set; }
    public string? Origen { get; set; }

    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }

    public bool? SoloActivos { get; set; }

    public string? Search { get; set; }
}