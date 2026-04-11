namespace Gv.Rh.Application.DTOs.Reclutamiento.Reportes;

public sealed class ReclutamientoReporteFiltroDto
{
    public int? VacanteId { get; set; }
    public string? EstatusVacante { get; set; }
    public int? CandidatoId { get; set; }
    public string? Etapa { get; set; }
    public int? SucursalId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}