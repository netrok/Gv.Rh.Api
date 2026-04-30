namespace Gv.Rh.Application.DTOs.Vacaciones.Import;

public sealed class VacacionesLegacyImportConfirmRequestDto
{
    public List<int> EmpleadoIdsConfirmados { get; set; } = new();

    public bool ImportarTodosElegibles { get; set; }

    public bool PermitirSaldosNegativos { get; set; }

    public string? Comentario { get; set; }
}
