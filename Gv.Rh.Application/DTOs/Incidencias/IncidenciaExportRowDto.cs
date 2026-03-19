namespace Gv.Rh.Application.DTOs.Incidencias;

public sealed class IncidenciaExportRowDto
{
    public int Id { get; set; }
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string Empleado { get; set; } = string.Empty;
    public string? Sucursal { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Estatus { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string? Comentario { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}