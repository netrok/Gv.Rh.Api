namespace Gv.Rh.Application.DTOs.Empleados;

public sealed class EmpleadoExportRowDto
{
    public int Id { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Sucursal { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string EstatusLaboral { get; set; } = string.Empty;
    public string Activo { get; set; } = string.Empty;
}