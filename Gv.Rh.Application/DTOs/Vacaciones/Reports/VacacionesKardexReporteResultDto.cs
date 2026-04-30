namespace Gv.Rh.Application.DTOs.Vacaciones.Reports;

public sealed class VacacionesKardexReporteResultDto
{
    public int TotalMovimientos { get; set; }

    public decimal TotalDiasPositivos { get; set; }
    public decimal TotalDiasNegativos { get; set; }
    public decimal BalanceDias { get; set; }

    public int MovimientosDisfrute { get; set; }
    public int MovimientosAjuste { get; set; }
    public int MovimientosImportacion { get; set; }

    public List<VacacionesKardexReporteRowDto> Items { get; set; } = [];
}