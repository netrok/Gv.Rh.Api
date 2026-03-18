using Gv.Rh.Domain.Common.Enums;

namespace Gv.Rh.Application.DTOs.Incidencias;

public class IncidenciaDto
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public string EmpleadoNombre { get; set; } = string.Empty;
    public int? SucursalId { get; set; }
    public string? SucursalNombre { get; set; }
    public TipoIncidencia Tipo { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string? Comentario { get; set; }
    public EstatusIncidencia Estatus { get; set; }

    public bool TieneEvidencia { get; set; }
    public string? EvidenciaNombreOriginal { get; set; }
    public string? EvidenciaContentType { get; set; }
    public long? EvidenciaTamanoBytes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}