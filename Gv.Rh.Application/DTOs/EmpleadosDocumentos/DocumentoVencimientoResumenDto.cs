namespace Gv.Rh.Application.DTOs.EmpleadosDocumentos;

public sealed class DocumentoVencimientoResumenDto
{
    public DateOnly FechaCorte { get; set; }
    public int DiasAnticipacion { get; set; }
    public int TotalPorVencer { get; set; }
    public int TotalVencidos { get; set; }
    public List<DocumentoVencimientoItemDto> PorVencer { get; set; } = [];
    public List<DocumentoVencimientoItemDto> Vencidos { get; set; } = [];
}