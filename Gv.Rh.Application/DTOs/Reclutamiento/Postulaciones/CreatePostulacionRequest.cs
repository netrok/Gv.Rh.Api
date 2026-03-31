using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Postulaciones;

public sealed class CreatePostulacionRequest
{
    [Required]
    public int VacanteId { get; set; }

    [Required]
    public int CandidatoId { get; set; }

    [MaxLength(2000)]
    public string? ObservacionesInternas { get; set; }
}