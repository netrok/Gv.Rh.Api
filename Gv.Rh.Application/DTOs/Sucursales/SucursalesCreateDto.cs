using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Api.DTOs.Sucursales;

public class SucursalCreateDto
{
    [Required]
    [MaxLength(20)]
    public string Clave { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Direccion { get; set; }

    [MaxLength(30)]
    public string? Telefono { get; set; }

    public bool Activo { get; set; } = true;
}