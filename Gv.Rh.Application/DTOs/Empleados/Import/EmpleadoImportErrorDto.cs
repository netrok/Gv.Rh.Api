namespace Gv.Rh.Application.DTOs.Empleados.Import;

public sealed class EmpleadoImportErrorDto
{
    public int RowNumber { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Value { get; set; }
}