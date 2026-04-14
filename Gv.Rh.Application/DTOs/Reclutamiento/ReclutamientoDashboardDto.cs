namespace Gv.Rh.Application.DTOs.Reclutamiento;

public sealed class ReclutamientoDashboardDto
{
    public int VacantesActivas { get; set; }
    public int VacantesCerradas { get; set; }
    public int CandidatosTotales { get; set; }
    public int CandidatosEnProceso { get; set; }
    public int ContratadosMes { get; set; }

    public List<DashboardCountItemDto> PipelinePorEtapa { get; set; } = [];
    public List<DashboardVacanteResumenDto> VacantesTop { get; set; } = [];
    public List<DashboardCandidatoResumenDto> CandidatosRecientes { get; set; } = [];
    public List<DashboardAlertaDto> Alertas { get; set; } = [];
}

public sealed class DashboardCountItemDto
{
    public string Nombre { get; set; } = string.Empty;
    public int Total { get; set; }
}

public sealed class DashboardVacanteResumenDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Departamento { get; set; }
    public string? Sucursal { get; set; }
    public string Estatus { get; set; } = string.Empty;
    public int TotalCandidatos { get; set; }
}

public sealed class DashboardCandidatoResumenDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Vacante { get; set; }
    public string Etapa { get; set; } = string.Empty;
    public DateTime FechaRegistroUtc { get; set; }
}

public sealed class DashboardAlertaDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? Total { get; set; }
}