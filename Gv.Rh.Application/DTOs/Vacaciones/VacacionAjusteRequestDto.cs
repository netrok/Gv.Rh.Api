namespace Gv.Rh.Application.DTOs.Vacaciones;

public class VacacionAjusteRequestDto
{
    public int VacacionPeriodoId { get; set; }
    public decimal Dias { get; set; }
    public DateOnly? FechaMovimiento { get; set; }
    public string? Referencia { get; set; }
    public string? Comentario { get; set; }
}
