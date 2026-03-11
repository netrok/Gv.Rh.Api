namespace Gv.Rh.Application.DTOs.Departamentos;

public class DepartamentoDto
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
