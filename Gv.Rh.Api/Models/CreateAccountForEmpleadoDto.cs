using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Api.Models;

public class CreateAccountForEmpleadoDto
{
    [Required, EmailAddress, StringLength(160)]
    public string Email { get; set; } = default!;

    [Required, StringLength(50)]
    public string Role { get; set; } = "RRHH";

    [Required, StringLength(200, MinimumLength = 10)]
    public string Password { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}