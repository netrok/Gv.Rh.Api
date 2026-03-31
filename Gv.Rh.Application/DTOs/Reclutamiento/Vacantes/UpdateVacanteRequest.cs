using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Vacantes;

public sealed class UpdateVacanteRequest
{
    [Required, MaxLength(150)]
    public string Titulo { get; set; } = string.Empty;

    [Required]
    public int DepartamentoId { get; set; }

    [Required]
    public int PuestoId { get; set; }

    [Required]
    public int SucursalId { get; set; }

    [Range(1, 999)]
    public int NumeroPosiciones { get; set; } = 1;

    [MaxLength(4000)]
    public string? Descripcion { get; set; }

    [MaxLength(4000)]
    public string? Perfil { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? SalarioMinimo { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? SalarioMaximo { get; set; }

    public DateOnly FechaApertura { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public bool Activo { get; set; } = true;
}