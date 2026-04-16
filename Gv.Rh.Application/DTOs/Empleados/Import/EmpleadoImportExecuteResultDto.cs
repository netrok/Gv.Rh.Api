namespace Gv.Rh.Application.DTOs.Empleados.Import;

public sealed class EmpleadoImportExecuteResultDto
{
    public int TotalRows { get; set; }
    public int InsertedRows { get; set; }
    public int SkippedRows { get; set; }
    public int ErrorRows { get; set; }
    public List<EmpleadoImportErrorDto> Errors { get; set; } = [];
}