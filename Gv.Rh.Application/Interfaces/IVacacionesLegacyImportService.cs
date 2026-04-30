using Gv.Rh.Application.DTOs.Vacaciones.Import;

namespace Gv.Rh.Application.Interfaces;

public interface IVacacionesLegacyImportService
{
    Task<VacacionesLegacyImportPreviewDto> PreviewAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<VacacionesLegacyImportConfirmResultDto> ConfirmAsync(
        Stream stream,
        string fileName,
        VacacionesLegacyImportConfirmRequestDto request,
        int? usuarioResponsableId,
        CancellationToken cancellationToken = default);
}
