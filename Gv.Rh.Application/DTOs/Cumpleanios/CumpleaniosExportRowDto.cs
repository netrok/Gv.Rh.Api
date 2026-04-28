namespace Gv.Rh.Application.DTOs.Cumpleanios;

public sealed class CumpleaniosExportRowDto
{
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string Empleado { get; set; } = string.Empty;

    public DateOnly? FechaNacimiento { get; set; }
    public DateOnly ProximoCumple { get; set; }
    public int EdadQueCumple { get; set; }
    public int DiasFaltantes { get; set; }

    public string? Puesto { get; set; }
    public string? Departamento { get; set; }
    public string? Sucursal { get; set; }

    public string? Email { get; set; }
    public string? Telefono { get; set; }
}