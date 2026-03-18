namespace Gv.Rh.Application.DTOs.Incidencias;

public sealed class IncidenciaEvidenciaDto
{
    public bool TieneEvidencia { get; set; }
    public string? EvidenciaNombreOriginal { get; set; }
    public string? EvidenciaContentType { get; set; }
    public long? EvidenciaTamanoBytes { get; set; }
}