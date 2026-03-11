using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Departamentos;

public class DepartamentoCreateDto
{
    [Required]
    [StringLength(20)]
    public string Clave { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;
}
