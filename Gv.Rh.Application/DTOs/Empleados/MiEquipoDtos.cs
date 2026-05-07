namespace Gv.Rh.Application.DTOs.Empleados;

public sealed class MiEquipoDto
{
    public int JefeEmpleadoId { get; set; }

    public string JefeNombre { get; set; } = string.Empty;

    public int Total { get; set; }

    public int ComoAprobadorPrimario { get; set; }

    public int ComoAprobadorSecundario { get; set; }

    public List<MiEquipoEmpleadoDto> Empleados { get; set; } = new();
}

public sealed class MiEquipoEmpleadoDto
{
    public int Id { get; set; }

    public string NumEmpleado { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Telefono { get; set; }

    public int? SucursalId { get; set; }

    public string? SucursalNombre { get; set; }

    public int? DepartamentoId { get; set; }

    public string? DepartamentoNombre { get; set; }

    public int? PuestoId { get; set; }

    public string? PuestoNombre { get; set; }

    public string TipoRelacion { get; set; } = string.Empty;

    public bool Activo { get; set; }

    public string EstatusLaboral { get; set; } = string.Empty;
}