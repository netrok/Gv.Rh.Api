using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Departamentos;

public class DepartamentoUpdateDto
{
    [Required]
    [StringLength(20)]
    public string Clave { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}