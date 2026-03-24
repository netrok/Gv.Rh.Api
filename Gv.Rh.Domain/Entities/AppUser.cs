using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class AppUser
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = UserRoles.Consulta;

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    // vínculo opcional con empleado
    public int? EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }
}