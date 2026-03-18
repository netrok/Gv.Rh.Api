namespace Gv.Rh.Application.DTOs.Dashboard;

public sealed class DashboardDto
{
    public int EmpleadosActivos { get; set; }
    public int SucursalesActivas { get; set; }
    public int IncidenciasPendientes { get; set; }
    public int IncidenciasMes { get; set; }

    public List<DashboardCountByDto> IncidenciasPorTipo { get; set; } = [];
    public List<DashboardCountByDto> IncidenciasPorEstatus { get; set; } = [];
    public List<DashboardIncidenciaRecienteDto> IncidenciasRecientes { get; set; } = [];
}