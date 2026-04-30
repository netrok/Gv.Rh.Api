namespace Gv.Rh.Application.DTOs.Vacaciones.Import;

public sealed class VacacionesLegacyImportConfirmItemDto
{
    public string Hoja { get; set; } = string.Empty;

    public int? EmpleadoId { get; set; }

    public string? NumEmpleado { get; set; }

    public string? NombreEmpleado { get; set; }

    public decimal? SaldoExcel { get; set; }

    public decimal? SaldoImportado { get; set; }

    public int? VacacionPeriodoId { get; set; }

    public int? VacacionMovimientoId { get; set; }

    public bool Importado { get; set; }

    public string Accion { get; set; } = string.Empty;

    public string? Mensaje { get; set; }

    public string? Error { get; set; }
}
