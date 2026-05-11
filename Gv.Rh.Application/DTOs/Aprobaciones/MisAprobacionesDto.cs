namespace Gv.Rh.Application.DTOs.Aprobaciones;

public sealed class MisAprobacionesDto
{
    public int TotalPendientes { get; set; }

    public int IncidenciasPendientes { get; set; }

    public int VacacionesPendientes { get; set; }

    public List<MisAprobacionesItemDto> Items { get; set; } = [];
}

public sealed class MisAprobacionesItemDto
{
    public string Tipo { get; set; } = string.Empty;

    public int Id { get; set; }

    public int EmpleadoId { get; set; }

    public string NumEmpleado { get; set; } = string.Empty;

    public string EmpleadoNombre { get; set; } = string.Empty;

    public DateOnly FechaInicio { get; set; }

    public DateOnly FechaFin { get; set; }

    public string Estatus { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public string UrlDetalle { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
