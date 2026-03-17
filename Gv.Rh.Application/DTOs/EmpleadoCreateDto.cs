using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Empleados;

public class EmpleadoCreateDto
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Nombres { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string ApellidoPaterno { get; set; } = string.Empty;

    [StringLength(120)]
    public string? ApellidoMaterno { get; set; }

    public DateOnly? FechaNacimiento { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    [EmailAddress, StringLength(160)]
    public string? Email { get; set; }

    [Required]
    public DateOnly FechaIngreso { get; set; }

    public bool Activo { get; set; } = true;

    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }
    public int? SucursalId { get; set; }
}