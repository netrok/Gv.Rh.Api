using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Domain.Entities;

public class Puesto
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string Clave { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    public int DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}