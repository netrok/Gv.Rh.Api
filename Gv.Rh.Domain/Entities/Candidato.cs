namespace Gv.Rh.Domain.Entities;

public class Candidato
{
    public int Id { get; set; }

    public string Nombres { get; set; } = string.Empty;
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string? ApellidoMaterno { get; set; }

    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string? Ciudad { get; set; }

    public string? FuenteReclutamiento { get; set; }
    public string? ResumenPerfil { get; set; }
    public decimal? PretensionSalarial { get; set; }

    public string? CvNombreOriginal { get; set; }
    public string? CvNombreGuardado { get; set; }
    public string? CvRutaRelativa { get; set; }
    public string? CvMimeType { get; set; }
    public long? CvTamanoBytes { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Postulacion> Postulaciones { get; set; } = new List<Postulacion>();
}