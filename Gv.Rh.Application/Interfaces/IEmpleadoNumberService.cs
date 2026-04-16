using System.Threading;
using System.Threading.Tasks;

namespace Gv.Rh.Application.Interfaces;

public interface IEmpleadoNumberService
{
    Task<string> GenerateNextAsync(CancellationToken cancellationToken = default);
    Task<string> PeekNextAsync(CancellationToken cancellationToken = default);
}
