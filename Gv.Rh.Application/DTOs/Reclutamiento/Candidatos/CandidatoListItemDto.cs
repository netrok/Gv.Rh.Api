namespace Gv.Rh.Application.DTOs.Reclutamiento.Candidatos;

public sealed class CandidatoListItemDto
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? FuenteReclutamiento { get; set; }
    public decimal? PretensionSalarial { get; set; }
    public bool TieneCv { get; set; }
    public bool Activo { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}