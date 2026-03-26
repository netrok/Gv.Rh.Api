namespace Gv.Rh.Application.DTOs.Dashboard;

public class DashboardDocumentoAlertaItemDto
{
    public int EmpleadoId { get; set; }

    public string NumEmpleado { get; set; } = string.Empty;

    public string NombreEmpleado { get; set; } = string.Empty;

    public string? DepartamentoNombre { get; set; }

    public string? PuestoNombre { get; set; }

    public string? SucursalNombre { get; set; }

    public int TotalFaltantes { get; set; }

    public int TotalPorVencer { get; set; }

    public int TotalVencidos { get; set; }

    public decimal PorcentajeCumplimiento { get; set; }
}