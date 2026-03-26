namespace Gv.Rh.Application.DTOs.Dashboard;

public class DashboardDocumentosResumenDto
{
    public int TotalEmpleadosActivos { get; set; }

    public int ExpedientesIncompletos { get; set; }

    public int DocumentosPorVencer { get; set; }

    public int DocumentosVencidos { get; set; }

    public List<DashboardDocumentoAlertaItemDto> Alertas { get; set; } = new();
}