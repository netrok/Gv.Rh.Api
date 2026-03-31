using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class PostulacionSeguimiento
{
    public int Id { get; set; }

    public int PostulacionId { get; set; }
    public Postulacion Postulacion { get; set; } = default!;

    public EtapaPostulacion? EtapaAnterior { get; set; }
    public EtapaPostulacion EtapaNueva { get; set; }

    public string? Comentario { get; set; }

    public int? CreadoPorUserId { get; set; }
    public AppUser? CreadoPorUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}