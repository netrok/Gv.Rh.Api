using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Domain.Entities;

public class Empleado
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string NumEmpleado { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string ApellidoPaterno { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? ApellidoMaterno { get; set; }

    public DateOnly? FechaNacimiento { get; set; }

    [MaxLength(30)]
    public string? Telefono { get; set; }

    [MaxLength(160)]
    public string? Email { get; set; }

    public DateOnly FechaIngreso { get; set; }

    public bool Activo { get; set; } = true;

    public int? DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }

    public int? PuestoId { get; set; }
    public Puesto? Puesto { get; set; }

    public int? SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    public ICollection<EmpleadoDocumento> Documentos { get; set; } = new List<EmpleadoDocumento>();
}