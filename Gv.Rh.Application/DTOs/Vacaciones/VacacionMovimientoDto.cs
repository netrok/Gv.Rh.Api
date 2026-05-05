using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Vacaciones;

public class VacacionMovimientoDto
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }

    public int VacacionPeriodoId { get; set; }

    public int CicloLaboral { get; set; }

    public int AnioServicio { get; set; }

    public TipoMovimientoVacacion TipoMovimiento { get; set; }

    public DateOnly FechaMovimiento { get; set; }

    public DateOnly? FechaInicioDisfrute { get; set; }

    public DateOnly? FechaFinDisfrute { get; set; }

    public decimal Dias { get; set; }

    public decimal SaldoAntes { get; set; }

    public decimal SaldoDespues { get; set; }

    public string? Referencia { get; set; }

    public string? Comentario { get; set; }

    public int? UsuarioResponsableId { get; set; }

    public string? Origen { get; set; }

    public string? ImportacionArchivo { get; set; }

    public string? ImportacionHoja { get; set; }

    public int? ImportacionFila { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}