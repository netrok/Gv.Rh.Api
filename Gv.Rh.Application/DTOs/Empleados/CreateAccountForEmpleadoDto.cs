using Gv.Rh.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Empleados;

public class CreateAccountForEmpleadoDto
{
    [Required]
    [EmailAddress]
    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Role { get; set; } = UserRoles.Empleado;

    [Required]
    [StringLength(200, MinimumLength = 10)]
    public string Password { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}