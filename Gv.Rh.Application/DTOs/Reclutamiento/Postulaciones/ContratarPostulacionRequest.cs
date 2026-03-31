using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Postulaciones;

public sealed class ContratarPostulacionRequest
{
    [MaxLength(2000)]
    public string? Comentario { get; set; }
}