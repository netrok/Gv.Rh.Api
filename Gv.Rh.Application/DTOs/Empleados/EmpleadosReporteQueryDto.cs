namespace Gv.Rh.Application.DTOs.Empleados;

public sealed class EmpleadosReporteQueryDto
{
    public int? SucursalId { get; set; }
    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }
    public bool? Activo { get; set; }
    public string? EstatusLaboral { get; set; }
    public DateOnly? FechaIngresoDesde { get; set; }
    public DateOnly? FechaIngresoHasta { get; set; }
    public string? Search { get; set; }
}