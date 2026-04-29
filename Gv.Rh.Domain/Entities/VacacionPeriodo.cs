using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class VacacionPeriodo
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }

    public Empleado? Empleado { get; set; }

    public int? VacacionPoliticaId { get; set; }

    public VacacionPolitica? VacacionPolitica { get; set; }

    public int AnioServicio { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly FechaFin { get; set; }

    public DateOnly FechaLimiteDisfrute { get; set; }

    public decimal DiasDerecho { get; set; }

    public decimal DiasTomados { get; set; }

    public decimal DiasPagados { get; set; }

    public decimal DiasAjustados { get; set; }

    public decimal DiasVencidos { get; set; }

    public decimal Saldo { get; set; }

    public bool PrimaPagada { get; set; }

    public DateOnly? FechaPagoPrima { get; set; }

    public string? Comentario { get; set; }

    public EstatusVacacionPeriodo Estatus { get; set; } =
        EstatusVacacionPeriodo.ABIERTO;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<VacacionMovimiento> Movimientos { get; set; } =
        new List<VacacionMovimiento>();
}