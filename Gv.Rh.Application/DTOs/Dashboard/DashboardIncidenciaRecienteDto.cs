namespace Gv.Rh.Application.DTOs.Dashboard;

public sealed class DashboardIncidenciaRecienteDto
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string EmpleadoNombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}