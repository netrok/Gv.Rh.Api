using Gv.Rh.Domain.Common.Enums;

namespace Gv.Rh.Application.DTOs.Incidencias;

public class UpdateIncidenciaDto
{
    public int EmpleadoId { get; set; }
    public int? SucursalId { get; set; }
    public TipoIncidencia Tipo { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string? Comentario { get; set; }
}