using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Postulaciones;

public sealed class PostulacionSeguimientoDto
{
    public int Id { get; set; }
    public EtapaPostulacion? EtapaAnterior { get; set; }
    public EtapaPostulacion EtapaNueva { get; set; }
    public string? Comentario { get; set; }
    public int? CreadoPorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}