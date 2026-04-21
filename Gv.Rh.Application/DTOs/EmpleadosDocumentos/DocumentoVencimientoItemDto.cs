namespace Gv.Rh.Application.DTOs.EmpleadosDocumentos;

public sealed class DocumentoVencimientoItemDto
{
    public long EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string EmpleadoNombreCompleto { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public DateOnly? FechaDocumento { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public int DiasParaVencer { get; set; }
    public bool EstaVencido { get; set; }
    public string? Comentario { get; set; }
}