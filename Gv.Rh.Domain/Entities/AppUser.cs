namespace Gv.Rh.Domain.Entities;

public class AppUser
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "ADMIN";
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Link opcional a empleado
    public int? EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }
}