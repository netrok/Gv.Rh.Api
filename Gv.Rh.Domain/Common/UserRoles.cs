namespace Gv.Rh.Domain.Common;

public static class UserRoles
{
    public const string Admin = "ADMIN";
    public const string Rrhh = "RRHH";
    public const string Jefe = "JEFE";
    public const string Consulta = "CONSULTA";

    public static readonly string[] All =
    {
        Admin,
        Rrhh,
        Jefe,
        Consulta
    };

    public static string Normalize(string? role)
        => (role ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? role)
        => All.Contains(Normalize(role));
}