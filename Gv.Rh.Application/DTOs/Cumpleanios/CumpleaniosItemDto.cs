namespace Gv.Rh.Application.DTOs.Cumpleanios;

public sealed class CumpleaniosItemDto
{
    public int EmpleadoId { get; set; }

    public string NumEmpleado { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public DateOnly? FechaNacimiento { get; set; }

    public int Dia { get; set; }

    public int Mes { get; set; }

    public int EdadQueCumple { get; set; }

    public string? SucursalNombre { get; set; }

    public string? DepartamentoNombre { get; set; }

    public string? PuestoNombre { get; set; }

    public string? FotoUrl { get; set; }

    public bool EsHoy { get; set; }

    public int DiasRestantes { get; set; }
}