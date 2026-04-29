namespace Gv.Rh.Application.DTOs.Vacaciones;

public class VacacionRegistrarDisfruteRequestDto
{
    public int VacacionPeriodoId { get; set; }
    public DateOnly FechaInicioDisfrute { get; set; }
    public DateOnly FechaFinDisfrute { get; set; }
    public decimal Dias { get; set; }
    public string? Referencia { get; set; }
    public string? Comentario { get; set; }
}
