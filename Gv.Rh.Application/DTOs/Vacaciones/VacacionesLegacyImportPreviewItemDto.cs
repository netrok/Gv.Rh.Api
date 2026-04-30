namespace Gv.Rh.Application.DTOs.Vacaciones.Import;

public sealed class VacacionesLegacyImportPreviewItemDto
{
    public string Hoja { get; set; } = string.Empty;
    public int? FilaReferencia { get; set; }

    public bool EmpleadoEncontrado { get; set; }
    public int? EmpleadoId { get; set; }
    public string? NumEmpleadoExcel { get; set; }
    public string? NumEmpleadoSistema { get; set; }
    public string? NombreExcel { get; set; }
    public string? NombreSistema { get; set; }
    public string? RfcExcel { get; set; }
    public string? NssExcel { get; set; }
    public string? PuestoExcel { get; set; }
    public string? FechaIngresoExcel { get; set; }

    public int? AnioServicioSugerido { get; set; }
    public decimal? DiasDerechoExcel { get; set; }
    public decimal? DiasDisfrutadosExcel { get; set; }
    public decimal? DiasPagadosExcel { get; set; }
    public decimal? SaldoExcel { get; set; }
    public decimal? SaldoSistemaActual { get; set; }
    public decimal? Diferencia { get; set; }

    public bool TienePeriodoSistema { get; set; }
    public string AccionSugerida { get; set; } = "REVISION_MANUAL";
    public bool PuedeImportar { get; set; }
    public string? Error { get; set; }
    public string? ObservacionesOriginales { get; set; }
}
