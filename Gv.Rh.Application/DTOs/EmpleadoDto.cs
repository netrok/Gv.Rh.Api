using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Empleados;

public class EmpleadoDto
{
    public int Id { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string? ApellidoMaterno { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public bool Activo { get; set; }

    public EstatusLaboralEmpleado EstatusLaboralActual { get; set; }
    public DateOnly? FechaBajaActual { get; set; }
    public TipoBajaEmpleado? TipoBajaActual { get; set; }
    public DateOnly? FechaReingresoActual { get; set; }
    public bool? Recontratable { get; set; }

    public int? DepartamentoId { get; set; }
    public string? DepartamentoNombre { get; set; }

    public int? PuestoId { get; set; }
    public string? PuestoNombre { get; set; }

    public int? SucursalId { get; set; }
    public string? SucursalNombre { get; set; }
}