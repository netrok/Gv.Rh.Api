namespace Gv.Rh.Application.DTOs.Dashboard;

public sealed class MiDashboardDto
{
    public MiDashboardEmpleadoDto Empleado { get; set; } = new();
    public MiDashboardDocumentosDto Documentos { get; set; } = new();
    public MiDashboardIncidenciasDto Incidencias { get; set; } = new();
}

public sealed class MiDashboardEmpleadoDto
{
    public int Id { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? PuestoNombre { get; set; }
    public string? DepartamentoNombre { get; set; }
    public string? SucursalNombre { get; set; }
    public bool Activo { get; set; }
}

public sealed class MiDashboardDocumentosDto
{
    public int Requeridos { get; set; }
    public int Cargados { get; set; }
    public int Faltantes { get; set; }
    public int PorVencer { get; set; }
    public int Vencidos { get; set; }
    public bool Completo { get; set; }
}

public sealed class MiDashboardIncidenciasDto
{
    public int Total { get; set; }
    public int Pendientes { get; set; }
    public int Aprobadas { get; set; }
    public int Rechazadas { get; set; }
}