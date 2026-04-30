namespace Gv.Rh.Application.DTOs.Vacaciones.Import;

public sealed class VacacionesLegacyImportConfirmResultDto
{
    public string Archivo { get; set; } = string.Empty;

    public int TotalPreviewItems { get; set; }

    public int Solicitados { get; set; }

    public int Importados { get; set; }

    public int Omitidos { get; set; }

    public int Errores { get; set; }

    public List<string> Advertencias { get; set; } = new();

    public List<VacacionesLegacyImportConfirmItemDto> Items { get; set; } = new();
}
