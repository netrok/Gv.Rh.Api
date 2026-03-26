namespace Gv.Rh.Application.DTOs.EmpleadosDocumentos;

public class EmpleadoDocumentoChecklistItemDto
{
    public int Tipo { get; set; }

    public string TipoNombre { get; set; } = string.Empty;

    public bool Requerido { get; set; }

    public bool TieneDocumento { get; set; }

    public string Estatus { get; set; } = string.Empty;

    public int? DocumentoId { get; set; }

    public string? NombreArchivoOriginal { get; set; }

    public DateOnly? FechaDocumento { get; set; }

    public DateOnly? FechaVencimiento { get; set; }
}