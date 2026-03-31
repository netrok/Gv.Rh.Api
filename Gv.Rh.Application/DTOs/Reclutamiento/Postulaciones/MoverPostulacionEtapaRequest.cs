using System.ComponentModel.DataAnnotations;
using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Postulaciones;

public sealed class MoverPostulacionEtapaRequest
{
    [Required]
    public EtapaPostulacion EtapaNueva { get; set; }

    [MaxLength(2000)]
    public string? Comentario { get; set; }
}