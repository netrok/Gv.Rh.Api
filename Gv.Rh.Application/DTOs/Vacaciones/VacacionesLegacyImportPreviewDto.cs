namespace Gv.Rh.Application.DTOs.Vacaciones.Import;

public sealed class VacacionesLegacyImportPreviewDto
{
    public string Archivo { get; set; } = string.Empty;
    public int TotalHojas { get; set; }
    public int HojasAnalizadas { get; set; }
    public int EmpleadosDetectados { get; set; }
    public int EmpleadosNoEncontrados { get; set; }
    public int ConDiferencias { get; set; }
    public List<string> Advertencias { get; set; } = [];
    public List<VacacionesLegacyImportPreviewItemDto> Items { get; set; } = [];
}
