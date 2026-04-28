namespace Gv.Rh.Domain.Common;

public static class UserRoles
{
    public const string Admin = "ADMIN";
    public const string Rrhh = "RRHH";
    public const string Jefe = "JEFE";
    public const string Empleado = "EMPLEADO";
    public const string Consulta = "CONSULTA";

    public static readonly string[] All =
    {
        Admin,
        Rrhh,
        Jefe,
        Empleado,
        Consulta
    };

    public static string Normalize(string? role)
        => (role ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? role)
        => All.Contains(Normalize(role));

    public static bool IsAdmin(string? role)
        => Normalize(role) == Admin;

    public static bool IsRrhh(string? role)
        => Normalize(role) == Rrhh;

    public static bool IsJefe(string? role)
        => Normalize(role) == Jefe;

    public static bool IsEmpleado(string? role)
        => Normalize(role) == Empleado;

    public static bool IsConsulta(string? role)
        => Normalize(role) == Consulta;

    public static bool CanManageCatalogs(string? role)
        => IsAdmin(role) || IsRrhh(role);

    public static bool CanApproveOperationalRequests(string? role)
        => IsAdmin(role) || IsRrhh(role) || IsJefe(role);
}