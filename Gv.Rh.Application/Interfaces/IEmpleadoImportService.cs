using Gv.Rh.Application.DTOs.Empleados.Import;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gv.Rh.Application.Interfaces;

public interface IEmpleadoImportService
{
    Task<byte[]> BuildTemplateAsync(CancellationToken cancellationToken = default);

    Task<EmpleadoImportValidateResultDto> ValidateAsync(
        Stream stream,
        CancellationToken cancellationToken = default);

    Task<EmpleadoImportExecuteResultDto> ImportAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}