using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class VacacionMovimiento
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }

    public Empleado? Empleado { get; set; }

    public int VacacionPeriodoId { get; set; }

    public VacacionPeriodo? VacacionPeriodo { get; set; }

    public TipoMovimientoVacacion TipoMovimiento { get; set; }

    public DateOnly FechaMovimiento { get; set; }

    public DateOnly? FechaInicioDisfrute { get; set; }

    public DateOnly? FechaFinDisfrute { get; set; }

    /// <summary>
    /// Días del movimiento. Puede ser positivo o negativo según el tipo.
    /// Ejemplo: APERTURA +12, DISFRUTE -3, AJUSTE_POSITIVO +1.
    /// </summary>
    public decimal Dias { get; set; }

    public decimal SaldoAntes { get; set; }

    public decimal SaldoDespues { get; set; }

    public string? Referencia { get; set; }

    public string? Comentario { get; set; }

    public int? UsuarioResponsableId { get; set; }

    public AppUser? UsuarioResponsable { get; set; }

    public string? Origen { get; set; }

    public string? ImportacionArchivo { get; set; }

    public string? ImportacionHoja { get; set; }

    public int? ImportacionFila { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}