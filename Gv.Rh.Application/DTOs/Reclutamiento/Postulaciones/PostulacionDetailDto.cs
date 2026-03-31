using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Postulaciones;

public sealed class PostulacionDetailDto
{
    public int Id { get; set; }

    public int VacanteId { get; set; }
    public string VacanteFolio { get; set; } = string.Empty;
    public string VacanteTitulo { get; set; } = string.Empty;

    public int CandidatoId { get; set; }
    public string CandidatoNombre { get; set; } = string.Empty;
    public string? CandidatoTelefono { get; set; }
    public string? CandidatoEmail { get; set; }

    public EtapaPostulacion EtapaActual { get; set; }

    public DateTime FechaPostulacionUtc { get; set; }
    public DateTime FechaUltimoMovimientoUtc { get; set; }

    public string? ObservacionesInternas { get; set; }
    public string? MotivoDescarte { get; set; }

    public bool EsContratado { get; set; }
    public int? EmpleadoId { get; set; }
    public DateTime? FechaContratacionUtc { get; set; }

    public bool Activo { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public List<PostulacionSeguimientoDto> Seguimiento { get; set; } = [];
}