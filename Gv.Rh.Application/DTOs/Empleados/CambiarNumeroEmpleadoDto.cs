using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Empleados;

public class CambiarNumeroEmpleadoDto
{
    [Required]
    [MaxLength(30)]
    public string NumEmpleadoNuevo { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Motivo { get; set; } = string.Empty;
}