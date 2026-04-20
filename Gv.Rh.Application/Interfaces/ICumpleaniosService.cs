using Gv.Rh.Application.DTOs.Cumpleanios;

namespace Gv.Rh.Application.Interfaces;

public interface ICumpleaniosService
{
    Task<IReadOnlyList<CumpleaniosItemDto>> GetHoyAsync(
        int? sucursalId,
        int? departamentoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CumpleaniosItemDto>> GetProximosAsync(
        int dias,
        int? sucursalId,
        int? departamentoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CumpleaniosItemDto>> GetMesAsync(
        int anio,
        int mes,
        int? sucursalId,
        int? departamentoId,
        CancellationToken cancellationToken = default);

    Task<CumpleaniosResumenDto> GetResumenAsync(
        int? sucursalId,
        int? departamentoId,
        CancellationToken cancellationToken = default);
}