namespace Gv.Rh.Application.DTOs.Cumpleanios;

public sealed class CumpleaniosReporteQueryDto
{
    public int? SucursalId { get; set; }
    public int? DepartamentoId { get; set; }

    // hoy | 7dias | 30dias | mes | custom
    public string? Scope { get; set; }

    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}