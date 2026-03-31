using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Postulaciones;

public sealed class PostulacionListItemDto
{
    public int Id { get; set; }

    public int VacanteId { get; set; }
    public string VacanteFolio { get; set; } = string.Empty;
    public string VacanteTitulo { get; set; } = string.Empty;

    public int CandidatoId { get; set; }
    public string CandidatoNombre { get; set; } = string.Empty;

    public EtapaPostulacion EtapaActual { get; set; }
    public DateTime FechaPostulacionUtc { get; set; }
    public DateTime FechaUltimoMovimientoUtc { get; set; }

    public bool EsContratado { get; set; }
    public int? EmpleadoId { get; set; }
    public bool Activo { get; set; }
}