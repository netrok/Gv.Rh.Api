namespace Gv.Rh.Application.DTOs.Vacaciones.Import;

public sealed class VacacionesLegacyConciliacionDto
{
    public string Archivo { get; set; } = string.Empty;

    public int TotalItemsPreview { get; set; }
    public int Encontrados { get; set; }
    public int NoEncontrados { get; set; }
    public int PosiblesCoincidencias { get; set; }
    public int ConDiferencias { get; set; }

    public List<string> Advertencias { get; set; } = [];
    public List<VacacionesLegacyConciliacionItemDto> Items { get; set; } = [];
}

public sealed class VacacionesLegacyConciliacionItemDto
{
    public string Hoja { get; set; } = string.Empty;
    public int? FilaReferencia { get; set; }

    public string Estado { get; set; } = "NO_ENCONTRADO";
    public string AccionSugerida { get; set; } = "REVISION_MANUAL";

    public int? EmpleadoId { get; set; }

    public string? NumEmpleadoExcel { get; set; }
    public string? NombreExcel { get; set; }
    public string? RfcExcel { get; set; }
    public string? NssExcel { get; set; }
    public string? PuestoExcel { get; set; }
    public string? FechaIngresoExcel { get; set; }

    public string? NumEmpleadoSistema { get; set; }
    public string? NombreSistema { get; set; }
    public string? RfcSistema { get; set; }
    public string? NssSistema { get; set; }
    public string? EstatusLaboralSistema { get; set; }

    public decimal? SaldoExcel { get; set; }
    public decimal? SaldoSistemaActual { get; set; }
    public decimal? DiferenciaSaldo { get; set; }

    public bool TienePeriodoSistema { get; set; }
    public bool PuedeImportar { get; set; }

    public string? Error { get; set; }
    public string? ObservacionesOriginales { get; set; }

    public List<string> Diferencias { get; set; } = [];
    public List<VacacionesLegacyConciliacionCandidatoDto> PosiblesCoincidencias { get; set; } = [];
}

public sealed class VacacionesLegacyConciliacionCandidatoDto
{
    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Rfc { get; set; }
    public string? Nss { get; set; }
    public string EstatusLaboral { get; set; } = string.Empty;

    public int Puntaje { get; set; }
    public List<string> Motivos { get; set; } = [];
}