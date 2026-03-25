using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class EmpleadoDocumento
{
    public int Id { get; set; }

    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;

    public TipoDocumentoEmpleado Tipo { get; set; }

    public string NombreArchivoOriginal { get; set; } = string.Empty;
    public string NombreArchivoGuardado { get; set; } = string.Empty;
    public string RutaRelativa { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;

    public long TamanoBytes { get; set; }

    public DateOnly? FechaDocumento { get; set; }
    public DateOnly? FechaVencimiento { get; set; }

    public string? Comentario { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}