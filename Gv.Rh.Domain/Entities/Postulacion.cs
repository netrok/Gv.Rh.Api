using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class Postulacion
{
    public int Id { get; set; }

    public int VacanteId { get; set; }
    public Vacante Vacante { get; set; } = default!;

    public int CandidatoId { get; set; }
    public Candidato Candidato { get; set; } = default!;

    public EtapaPostulacion EtapaActual { get; set; } = EtapaPostulacion.POSTULADO;

    public DateTime FechaPostulacionUtc { get; set; } = DateTime.UtcNow;
    public DateTime FechaUltimoMovimientoUtc { get; set; } = DateTime.UtcNow;

    public string? ObservacionesInternas { get; set; }
    public string? MotivoDescarte { get; set; }

    public bool EsContratado { get; set; } = false;

    public int? EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }

    public DateTime? FechaContratacionUtc { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<PostulacionSeguimiento> Seguimientos { get; set; } = new List<PostulacionSeguimiento>();
}