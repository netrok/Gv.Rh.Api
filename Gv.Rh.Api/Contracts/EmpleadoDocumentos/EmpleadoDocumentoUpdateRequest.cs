using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Api.Contracts.EmpleadoDocumentos;

public class EmpleadoDocumentoUpdateRequest
{
    [Required]
    public int Tipo { get; set; }

    public DateOnly? FechaDocumento { get; set; }

    public DateOnly? FechaVencimiento { get; set; }

    [MaxLength(1000)]
    public string? Comentario { get; set; }
}
