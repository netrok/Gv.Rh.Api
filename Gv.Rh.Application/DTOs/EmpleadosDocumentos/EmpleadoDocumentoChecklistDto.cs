namespace Gv.Rh.Application.DTOs.EmpleadosDocumentos;

public class EmpleadoDocumentoChecklistDto
{
    public int EmpleadoId { get; set; }

    public int TotalRequeridos { get; set; }
    public int TotalCargados { get; set; }
    public int TotalFaltantes { get; set; }
    public int TotalPorVencer { get; set; }
    public int TotalVencidos { get; set; }

    public decimal PorcentajeCumplimiento { get; set; }

    public List<EmpleadoDocumentoChecklistItemDto> Items { get; set; } = [];
}
