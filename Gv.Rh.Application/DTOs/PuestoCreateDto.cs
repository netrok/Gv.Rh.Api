using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Puestos;

public class PuestoCreateDto
{
    [Required]
    [StringLength(20)]
    public string Clave { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    public int DepartamentoId { get; set; }
}