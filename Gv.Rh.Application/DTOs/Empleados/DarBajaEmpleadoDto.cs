using Gv.Rh.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Empleados;

public class DarBajaEmpleadoDto
{
    [Required]
    public DateOnly FechaBaja { get; set; }

    [Required]
    public TipoBajaEmpleado TipoBaja { get; set; }

    [MaxLength(200)]
    public string? Motivo { get; set; }

    [MaxLength(1000)]
    public string? Comentario { get; set; }

    public bool? Recontratable { get; set; }

    public bool DesactivarUsuario { get; set; } = true;
}