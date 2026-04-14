namespace Gv.Rh.Application.DTOs.Empleados;

public sealed class EmpleadoFichaDto
{
    public int Id { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;

    public string? Nombres { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }

    public DateOnly? FechaNacimiento { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    public string? Sucursal { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }

    public DateOnly FechaIngreso { get; set; }
    public string EstatusLaboralActual { get; set; } = string.Empty;
    public bool Activo { get; set; }

    public DateOnly? FechaBajaActual { get; set; }
    public string? TipoBajaActual { get; set; }
    public DateOnly? FechaReingresoActual { get; set; }
    public bool? Recontratable { get; set; }
}