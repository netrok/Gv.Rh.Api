using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Vacaciones;

public class VacacionPeriodoDto
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }

    public int? VacacionPoliticaId { get; set; }

    public string? VacacionPoliticaNombre { get; set; }

    public int CicloLaboral { get; set; }

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

    public EstatusVacacionPeriodo Estatus { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}