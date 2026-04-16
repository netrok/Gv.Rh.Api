namespace Gv.Rh.Application.DTOs.Empleados.Import;

public sealed class EmpleadoImportValidateResultDto
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }
    public bool CanImport => ErrorRows == 0;
    public List<EmpleadoImportErrorDto> Errors { get; set; } = [];
}