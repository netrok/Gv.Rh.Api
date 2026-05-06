using Gv.Rh.Application.DTOs.Vacaciones.Dashboard;

namespace Gv.Rh.Application.Interfaces;

public interface IVacacionesDashboardService
{
    Task<VacacionesDashboardDto> GetDashboardAsync(
        CancellationToken cancellationToken = default);
}
