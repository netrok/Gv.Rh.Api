namespace Gv.Rh.Domain.Entities;

public class VacacionPolitica
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public DateOnly VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public decimal PrimaVacacionalPorcentaje { get; set; } = 25m;

    public bool Activo { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<VacacionPoliticaDetalle> Detalles { get; set; } =
        new List<VacacionPoliticaDetalle>();

    public ICollection<VacacionPeriodo> Periodos { get; set; } =
        new List<VacacionPeriodo>();
}