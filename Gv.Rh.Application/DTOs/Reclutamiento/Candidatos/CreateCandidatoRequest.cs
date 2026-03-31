using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Candidatos;

public sealed class CreateCandidatoRequest
{
    [Required, MaxLength(150)]
    public string Nombres { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string ApellidoPaterno { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? ApellidoMaterno { get; set; }

    [MaxLength(30)]
    public string? Telefono { get; set; }

    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }

    public DateOnly? FechaNacimiento { get; set; }

    [MaxLength(150)]
    public string? Ciudad { get; set; }

    [MaxLength(100)]
    public string? FuenteReclutamiento { get; set; }

    [MaxLength(4000)]
    public string? ResumenPerfil { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PretensionSalarial { get; set; }

    public bool Activo { get; set; } = true;
}