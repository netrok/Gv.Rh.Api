using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;

namespace Gv.Rh.Application.Interfaces;

public sealed record EmpleadoLaboralSnapshot(
    int EmpleadoId,
    int? PuestoId,
    int? DepartamentoId,
    int? SucursalId,
    EstatusLaboralEmpleado EstatusLaboralActual,
    bool Activo);

public interface IEmpleadoMovimientoLaboralService
{
    Task RegistrarAltaAsync(
        Empleado empleado,
        int? usuarioResponsableId,
        CancellationToken cancellationToken);

    Task RegistrarCambiosAsync(
        EmpleadoLaboralSnapshot anterior,
        Empleado actual,
        int? usuarioResponsableId,
        CancellationToken cancellationToken);

    Task RegistrarBajaAsync(
        Empleado empleado,
        DateOnly fechaBaja,
        TipoBajaEmpleado? tipoBaja,
        string? motivo,
        string? comentario,
        bool? recontratable,
        int? usuarioResponsableId,
        CancellationToken cancellationToken);

    Task RegistrarReingresoAsync(
        Empleado empleado,
        DateOnly fechaReingreso,
        string? comentario,
        int? usuarioResponsableId,
        CancellationToken cancellationToken);
}