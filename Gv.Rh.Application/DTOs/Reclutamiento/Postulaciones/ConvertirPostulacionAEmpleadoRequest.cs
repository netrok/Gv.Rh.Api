using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Postulaciones;

public sealed class ConvertirPostulacionAEmpleadoRequest
{
    [Required]
    public int DepartamentoId { get; set; }

    [Required]
    public int PuestoId { get; set; }

    [Required]
    public int SucursalId { get; set; }

    [Required]
    public DateOnly FechaIngreso { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public bool Activo { get; set; } = true;
}