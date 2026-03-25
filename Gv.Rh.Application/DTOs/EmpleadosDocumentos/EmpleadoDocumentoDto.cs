namespace Gv.Rh.Application.DTOs.EmpleadosDocumentos;

public class EmpleadoDocumentoDto
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }

    public int Tipo { get; set; }
    public string TipoNombre { get; set; } = string.Empty;

    public string NombreArchivoOriginal { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }

    public DateOnly? FechaDocumento { get; set; }
    public DateOnly? FechaVencimiento { get; set; }

    public string? Comentario { get; set; }

    public bool Activo { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}