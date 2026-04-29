namespace Gv.Rh.Domain.Entities;

public class VacacionPoliticaDetalle
{
    public int Id { get; set; }

    public int VacacionPoliticaId { get; set; }

    public VacacionPolitica? VacacionPolitica { get; set; }

    public int AnioServicioDesde { get; set; }

    public int? AnioServicioHasta { get; set; }

    public decimal DiasVacaciones { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}