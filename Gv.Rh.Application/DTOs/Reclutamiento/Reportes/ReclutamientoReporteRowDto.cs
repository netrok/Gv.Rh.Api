namespace Gv.Rh.Application.DTOs.Reclutamiento.Reportes;

public sealed class ReclutamientoReporteRowDto
{
    public int VacanteId { get; set; }
    public string VacanteTitulo { get; set; } = string.Empty;

    public int? SucursalId { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }
    public string? Sucursal { get; set; }
    public string? EstatusVacante { get; set; }

    public int CandidatoId { get; set; }
    public string CandidatoNombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Correo { get; set; }

    public string? EtapaActual { get; set; }
    public string? EstatusCandidato { get; set; }

    public DateTime? FechaPostulacionUtc { get; set; }
    public DateTime? FechaUltimaActualizacionUtc { get; set; }
    public string? Resultado { get; set; }
}