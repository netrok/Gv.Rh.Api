namespace Gv.Rh.Domain.Entities;

public class Empleado
{
    public int Id { get; set; }
    public string NumEmpleado { get; set; } = default!;
    public string Nombres { get; set; } = default!;
    public string ApellidoPaterno { get; set; } = default!;
    public string? ApellidoMaterno { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public bool Activo { get; set; } = true;
}