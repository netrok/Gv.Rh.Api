using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.EmpleadosDocumentos;

public class EmpleadoDocumentoUpdateRequest
{
    [Required]
    public int Tipo { get; set; }

    public DateOnly? FechaDocumento { get; set; }

    public DateOnly? FechaVencimiento { get; set; }

    [StringLength(1000)]
    public string? Comentario { get; set; }
}