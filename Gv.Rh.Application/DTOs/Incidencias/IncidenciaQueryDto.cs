using Gv.Rh.Domain.Common.Enums;

namespace Gv.Rh.Application.DTOs.Incidencias;

public class IncidenciaQueryDto
{
    public int? EmpleadoId { get; set; }
    public int? SucursalId { get; set; }
    public TipoIncidencia? Tipo { get; set; }
    public EstatusIncidencia? Estatus { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public bool? SoloPendientes { get; set; }
}