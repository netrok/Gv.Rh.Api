using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Gv.Rh.Api.Contracts.EmpleadoDocumentos;

public class EmpleadoDocumentoCreateRequest
{
    [Required]
    public int Tipo { get; set; }

    public DateOnly? FechaDocumento { get; set; }

    public DateOnly? FechaVencimiento { get; set; }

    [StringLength(1000)]
    public string? Comentario { get; set; }

    [Required]
    public IFormFile Archivo { get; set; } = null!;
}