namespace Gv.Rh.Application.DTOs.Reclutamiento.Candidatos;

public sealed class CandidatoDetailDto
{
    public int Id { get; set; }

    public string Nombres { get; set; } = string.Empty;
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string? ApellidoMaterno { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;

    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string? Ciudad { get; set; }

    public string? FuenteReclutamiento { get; set; }
    public string? ResumenPerfil { get; set; }
    public decimal? PretensionSalarial { get; set; }

    public bool TieneCv { get; set; }
    public string? CvNombreOriginal { get; set; }
    public string? CvMimeType { get; set; }
    public long? CvTamanoBytes { get; set; }

    public bool Activo { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}