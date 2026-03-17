namespace Gv.Rh.Api.DTOs.Sucursales;

public class SucursalDto
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; }
    public int EmpleadosActivos { get; set; }
}