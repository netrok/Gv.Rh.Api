using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Postulaciones;

public sealed class DescartarPostulacionRequest
{
    [Required, MaxLength(1000)]
    public string MotivoDescarte { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Comentario { get; set; }
}