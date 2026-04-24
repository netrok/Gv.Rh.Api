using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Services;

public sealed class EmpleadoMovimientoLaboralService : IEmpleadoMovimientoLaboralService
{
    private readonly RhDbContext _db;

    public EmpleadoMovimientoLaboralService(RhDbContext db)
    {
        _db = db;
    }

    public async Task RegistrarAltaAsync(
        Empleado empleado,
        int? usuarioResponsableId,
        CancellationToken cancellationToken)
    {
        var puesto = await ResolvePuestoNombreAsync(empleado.PuestoId, cancellationToken);
        var sucursal = await ResolveSucursalNombreAsync(empleado.SucursalId, cancellationToken);

        _db.EmpleadoMovimientosLaborales.Add(new EmpleadoMovimientoLaboral
        {
            EmpleadoId = empleado.Id,
            TipoMovimiento = TipoMovimientoLaboral.ALTA,
            FechaMovimiento = empleado.FechaIngreso,
            Motivo = "Alta inicial",
            Comentario = $"Ingreso inicial en {sucursal} como {puesto}.",
            Recontratable = empleado.Recontratable,
            UsuarioResponsableId = usuarioResponsableId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RegistrarCambiosAsync(
        EmpleadoLaboralSnapshot anterior,
        Empleado actual,
        int? usuarioResponsableId,
        CancellationToken cancellationToken)
    {
        var movimientos = new List<EmpleadoMovimientoLaboral>();

        if (anterior.PuestoId != actual.PuestoId)
        {
            var anteriorNombre = await ResolvePuestoNombreAsync(anterior.PuestoId, cancellationToken);
            var actualNombre = await ResolvePuestoNombreAsync(actual.PuestoId, cancellationToken);

            movimientos.Add(new EmpleadoMovimientoLaboral
            {
                EmpleadoId = actual.Id,
                TipoMovimiento = TipoMovimientoLaboral.CAMBIO_PUESTO,
                FechaMovimiento = DateOnly.FromDateTime(DateTime.Today),
                Motivo = "Cambio de puesto",
                Comentario = $"Cambio de puesto de {anteriorNombre} a {actualNombre}.",
                UsuarioResponsableId = usuarioResponsableId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        if (anterior.DepartamentoId != actual.DepartamentoId)
        {
            var anteriorNombre = await ResolveDepartamentoNombreAsync(anterior.DepartamentoId, cancellationToken);
            var actualNombre = await ResolveDepartamentoNombreAsync(actual.DepartamentoId, cancellationToken);

            movimientos.Add(new EmpleadoMovimientoLaboral
            {
                EmpleadoId = actual.Id,
                TipoMovimiento = TipoMovimientoLaboral.CAMBIO_DEPARTAMENTO,
                FechaMovimiento = DateOnly.FromDateTime(DateTime.Today),
                Motivo = "Cambio de departamento",
                Comentario = $"Cambio de departamento de {anteriorNombre} a {actualNombre}.",
                UsuarioResponsableId = usuarioResponsableId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        if (anterior.SucursalId != actual.SucursalId)
        {
            var anteriorNombre = await ResolveSucursalNombreAsync(anterior.SucursalId, cancellationToken);
            var actualNombre = await ResolveSucursalNombreAsync(actual.SucursalId, cancellationToken);

            movimientos.Add(new EmpleadoMovimientoLaboral
            {
                EmpleadoId = actual.Id,
                TipoMovimiento = TipoMovimientoLaboral.CAMBIO_SUCURSAL,
                FechaMovimiento = DateOnly.FromDateTime(DateTime.Today),
                Motivo = "Cambio de sucursal",
                Comentario = $"Cambio de sucursal de {anteriorNombre} a {actualNombre}.",
                UsuarioResponsableId = usuarioResponsableId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        if (anterior.EstatusLaboralActual != actual.EstatusLaboralActual || anterior.Activo != actual.Activo)
        {
            movimientos.Add(new EmpleadoMovimientoLaboral
            {
                EmpleadoId = actual.Id,
                TipoMovimiento = TipoMovimientoLaboral.CAMBIO_ESTATUS,
                FechaMovimiento = DateOnly.FromDateTime(DateTime.Today),
                Motivo = "Cambio de estatus laboral",
                Comentario = $"Cambio de estatus de {anterior.EstatusLaboralActual} a {actual.EstatusLaboralActual}.",
                Recontratable = actual.Recontratable,
                UsuarioResponsableId = usuarioResponsableId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        if (movimientos.Count == 0)
            return;

        _db.EmpleadoMovimientosLaborales.AddRange(movimientos);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RegistrarBajaAsync(
        Empleado empleado,
        DateOnly fechaBaja,
        TipoBajaEmpleado? tipoBaja,
        string? motivo,
        string? comentario,
        bool? recontratable,
        int? usuarioResponsableId,
        CancellationToken cancellationToken)
    {
        _db.EmpleadoMovimientosLaborales.Add(new EmpleadoMovimientoLaboral
        {
            EmpleadoId = empleado.Id,
            TipoMovimiento = TipoMovimientoLaboral.BAJA,
            FechaMovimiento = fechaBaja,
            TipoBaja = tipoBaja,
            Motivo = motivo,
            Comentario = comentario,
            Recontratable = recontratable,
            UsuarioResponsableId = usuarioResponsableId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RegistrarReingresoAsync(
        Empleado empleado,
        DateOnly fechaReingreso,
        string? comentario,
        int? usuarioResponsableId,
        CancellationToken cancellationToken)
    {
        var puesto = await ResolvePuestoNombreAsync(empleado.PuestoId, cancellationToken);
        var sucursal = await ResolveSucursalNombreAsync(empleado.SucursalId, cancellationToken);

        _db.EmpleadoMovimientosLaborales.Add(new EmpleadoMovimientoLaboral
        {
            EmpleadoId = empleado.Id,
            TipoMovimiento = TipoMovimientoLaboral.REINGRESO,
            FechaMovimiento = fechaReingreso,
            Motivo = "Reingreso",
            Comentario = string.IsNullOrWhiteSpace(comentario)
                ? $"Reingreso en {sucursal} como {puesto}."
                : comentario.Trim(),
            Recontratable = true,
            UsuarioResponsableId = usuarioResponsableId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> ResolvePuestoNombreAsync(int? puestoId, CancellationToken cancellationToken)
    {
        if (!puestoId.HasValue)
            return "No registrado";

        return await _db.Puestos
            .AsNoTracking()
            .Where(x => x.Id == puestoId.Value)
            .Select(x => x.Nombre)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "No registrado";
    }

    private async Task<string> ResolveDepartamentoNombreAsync(int? departamentoId, CancellationToken cancellationToken)
    {
        if (!departamentoId.HasValue)
            return "No registrado";

        return await _db.Departamentos
            .AsNoTracking()
            .Where(x => x.Id == departamentoId.Value)
            .Select(x => x.Nombre)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "No registrado";
    }

    private async Task<string> ResolveSucursalNombreAsync(int? sucursalId, CancellationToken cancellationToken)
    {
        if (!sucursalId.HasValue)
            return "No registrado";

        return await _db.Sucursales
            .AsNoTracking()
            .Where(x => x.Id == sucursalId.Value)
            .Select(x => x.Nombre)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "No registrado";
    }
}