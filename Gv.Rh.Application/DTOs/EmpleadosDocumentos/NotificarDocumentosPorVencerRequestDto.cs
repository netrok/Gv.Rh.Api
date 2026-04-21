namespace Gv.Rh.Application.DTOs.EmpleadosDocumentos;

public sealed class NotificarDocumentosPorVencerRequestDto
{
    public int DiasAnticipacion { get; set; } = 30;
    public bool IncluirVencidos { get; set; } = true;
    public bool SoloPreview { get; set; } = false;
}