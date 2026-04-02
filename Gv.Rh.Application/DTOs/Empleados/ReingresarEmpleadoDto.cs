using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Empleados;

public class ReingresarEmpleadoDto
{
    [Required]
    public DateOnly FechaReingreso { get; set; }

    public int? DepartamentoId { get; set; }

    public int? PuestoId { get; set; }

    public int? SucursalId { get; set; }

    [MaxLength(1000)]
    public string? Comentario { get; set; }

    public bool ReactivarUsuario { get; set; } = true;
}