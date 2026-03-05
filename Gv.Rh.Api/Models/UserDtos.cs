using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Api.Models;

public class UserCreateDto
{
    [Required, EmailAddress, StringLength(160)]
    public string Email { get; set; } = default!;

    [Required, StringLength(50)]
    public string Role { get; set; } = "RRHH"; // default útil

    [Required, StringLength(200, MinimumLength = 10)]
    public string Password { get; set; } = default!;

    public int? EmpleadoId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UserUpdateDto
{
    [Required, StringLength(50)]
    public string Role { get; set; } = default!;

    public bool IsActive { get; set; }

    public int? EmpleadoId { get; set; }
}

public class ResetPasswordDto
{
    [StringLength(200, MinimumLength = 10)]
    public string? NewPassword { get; set; } // si viene null generamos temporal
}