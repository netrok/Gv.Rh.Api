using Gv.Rh.Application.DTOs.Vacaciones.Solicitudes;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Common.Enums;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Services;

public sealed class VacacionesSolicitudesService : IVacacionesSolicitudesService
{
    private readonly RhDbContext _db;
    private readonly IVacacionesSolicitudesNotificationService _notificationService;

    public VacacionesSolicitudesService(
        RhDbContext db,
        IVacacionesSolicitudesNotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    public async Task<VacacionesSolicitudListResultDto> GetSolicitudesAsync(
        VacacionesSolicitudQueryDto query,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, 200);

        var baseQuery = BuildVisibleQuery(actor);

        if (query.EmpleadoId.HasValue)
            baseQuery = baseQuery.Where(x => x.EmpleadoId == query.EmpleadoId.Value);

        if (query.SucursalId.HasValue)
            baseQuery = baseQuery.Where(x => x.Empleado != null && x.Empleado.SucursalId == query.SucursalId.Value);

        if (query.DepartamentoId.HasValue)
            baseQuery = baseQuery.Where(x => x.Empleado != null && x.Empleado.DepartamentoId == query.DepartamentoId.Value);

        if (query.PuestoId.HasValue)
            baseQuery = baseQuery.Where(x => x.Empleado != null && x.Empleado.PuestoId == query.PuestoId.Value);

        if (query.Estatus.HasValue)
            baseQuery = baseQuery.Where(x => x.Estatus == query.Estatus.Value);

        if (query.SoloPendientes == true)
            baseQuery = baseQuery.Where(x => x.Estatus == EstatusVacacionSolicitud.PENDIENTE);

        if (query.FechaDesde.HasValue)
            baseQuery = baseQuery.Where(x => x.FechaFin >= query.FechaDesde.Value);

        if (query.FechaHasta.HasValue)
            baseQuery = baseQuery.Where(x => x.FechaInicio <= query.FechaHasta.Value);

        var total = await baseQuery.CountAsync(cancellationToken);

        var counts = await BuildVisibleQuery(actor)
            .GroupBy(x => x.Estatus)
            .Select(x => new { Estatus = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => ToDtoProjection(x))
            .ToListAsync(cancellationToken);

        return new VacacionesSolicitudListResultDto
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
            TotalPages = total == 0
                ? 0
                : (int)Math.Ceiling(total / (decimal)query.PageSize),

            Pendientes = counts.FirstOrDefault(x => x.Estatus == EstatusVacacionSolicitud.PENDIENTE)?.Count ?? 0,
            Aprobadas = counts.FirstOrDefault(x => x.Estatus == EstatusVacacionSolicitud.APROBADA)?.Count ?? 0,
            Rechazadas = counts.FirstOrDefault(x => x.Estatus == EstatusVacacionSolicitud.RECHAZADA)?.Count ?? 0,
            Canceladas = counts.FirstOrDefault(x => x.Estatus == EstatusVacacionSolicitud.CANCELADA)?.Count ?? 0,

            Items = items
        };
    }

    public async Task<VacacionesSolicitudDto> GetSolicitudByIdAsync(
        int id,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default)
    {
        var solicitud = await BuildVisibleQuery(actor)
            .Where(x => x.Id == id)
            .Select(x => ToDtoProjection(x))
            .FirstOrDefaultAsync(cancellationToken);

        return solicitud ?? throw new KeyNotFoundException("La solicitud de vacaciones no existe o no tienes acceso.");
    }

    public async Task<VacacionesSolicitudDto> CreateSolicitudAsync(
        VacacionesSolicitudCreateDto request,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        var role = NormalizeRole(actor.Role);

        var empleadoId = request.EmpleadoId;

        if (IsEmpleadoOnly(role))
        {
            if (!actor.EmpleadoId.HasValue)
                throw new InvalidOperationException("Tu usuario no está vinculado a un empleado.");

            if (empleadoId.HasValue && empleadoId.Value != actor.EmpleadoId.Value)
                throw new UnauthorizedAccessException("No puedes crear solicitudes para otro empleado.");

            empleadoId = actor.EmpleadoId.Value;
        }

        if (!empleadoId.HasValue)
            throw new InvalidOperationException("Debes indicar el empleado.");

        var empleado = await _db.Empleados
            .Include(x => x.Sucursal)
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .FirstOrDefaultAsync(x => x.Id == empleadoId.Value, cancellationToken);

        if (empleado is null)
            throw new KeyNotFoundException("El empleado no existe.");

        if (!empleado.Activo || empleado.EstatusLaboralActual != EstatusLaboralEmpleado.ACTIVO)
            throw new InvalidOperationException("Solo se pueden solicitar vacaciones para empleados activos.");

        if (!CanCreateForEmployee(actor, empleado))
            throw new UnauthorizedAccessException("No tienes permiso para crear esta solicitud.");

        var periodo = await ResolvePeriodoAsync(
            empleado.Id,
            request.VacacionPeriodoId,
            request.DiasSolicitados,
            cancellationToken);

        var now = DateTime.UtcNow;

        var entity = new VacacionSolicitud
        {
            EmpleadoId = empleado.Id,
            VacacionPeriodoId = periodo.Id,
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            DiasSolicitados = request.DiasSolicitados,
            Estatus = EstatusVacacionSolicitud.PENDIENTE,
            ComentarioEmpleado = NormalizeNullable(request.ComentarioEmpleado),
            SolicitadaPorUsuarioId = actor.UsuarioId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.VacacionSolicitudes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetSolicitudByIdAsync(entity.Id, actor, cancellationToken);
    }

    public async Task<VacacionesSolicitudDto> AprobarSolicitudAsync(
        int id,
        VacacionesSolicitudResolverDto request,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default)
    {
        if (!CanResolve(actor))
            throw new UnauthorizedAccessException("No tienes permiso para aprobar solicitudes.");

        var solicitud = await LoadSolicitudForMutationAsync(id, cancellationToken);

        if (solicitud.Estatus != EstatusVacacionSolicitud.PENDIENTE)
            throw new InvalidOperationException("Solo se pueden aprobar solicitudes pendientes.");

        if (!CanResolveSolicitud(actor, solicitud))
            throw new UnauthorizedAccessException("No tienes permiso para aprobar esta solicitud.");

        var periodo = await _db.VacacionPeriodos
            .FirstOrDefaultAsync(
                x => x.Id == solicitud.VacacionPeriodoId &&
                     x.EmpleadoId == solicitud.EmpleadoId,
                cancellationToken);

        if (periodo is null)
            throw new KeyNotFoundException("El periodo de vacaciones asociado no existe.");

        if (periodo.Estatus != EstatusVacacionPeriodo.ABIERTO)
            throw new InvalidOperationException("Solo se pueden aprobar solicitudes sobre periodos abiertos.");

        if (periodo.Saldo < solicitud.DiasSolicitados)
            throw new InvalidOperationException("El periodo no tiene saldo suficiente para aprobar la solicitud.");

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var saldoAntes = periodo.Saldo;
        var saldoDespues = saldoAntes - solicitud.DiasSolicitados;

        periodo.DiasTomados += solicitud.DiasSolicitados;
        periodo.Saldo = saldoDespues;
        periodo.UpdatedAtUtc = now;

        var movimiento = new VacacionMovimiento
        {
            EmpleadoId = solicitud.EmpleadoId,
            VacacionPeriodoId = periodo.Id,
            CicloLaboral = periodo.CicloLaboral,
            TipoMovimiento = TipoMovimientoVacacion.DISFRUTE,
            FechaMovimiento = today,
            FechaInicioDisfrute = solicitud.FechaInicio,
            FechaFinDisfrute = solicitud.FechaFin,
            Dias = solicitud.DiasSolicitados,
            SaldoAntes = saldoAntes,
            SaldoDespues = saldoDespues,
            Referencia = $"SOL-VAC-{solicitud.Id}",
            Comentario = BuildMovimientoComentario(solicitud, request),
            UsuarioResponsableId = actor.UsuarioId,
            Origen = "SOLICITUD_VACACIONES",
            CreatedAtUtc = now
        };

        _db.VacacionMovimientos.Add(movimiento);
        await _db.SaveChangesAsync(cancellationToken);

        var incidencia = new Incidencia
        {
            EmpleadoId = solicitud.EmpleadoId,
            SucursalId = solicitud.Empleado?.SucursalId,
            Tipo = TipoIncidencia.VACACIONES,
            FechaInicio = solicitud.FechaInicio,
            FechaFin = solicitud.FechaFin,
            Comentario = $"Solicitud de vacaciones aprobada. Folio SOL-VAC-{solicitud.Id}. Días: {solicitud.DiasSolicitados:0.##}.",
            Estatus = EstatusIncidencia.APROBADA,
            ResueltaPorUsuarioId = actor.UsuarioId,
            ResueltaPorEmpleadoId = actor.EmpleadoId,
            FechaResolucionUtc = now,
            ComentarioResolucion = NormalizeNullable(request.ComentarioResolucion),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Incidencias.Add(incidencia);
        await _db.SaveChangesAsync(cancellationToken);

        solicitud.Estatus = EstatusVacacionSolicitud.APROBADA;
        solicitud.ResueltaPorUsuarioId = actor.UsuarioId;
        solicitud.AprobadorEmpleadoId = actor.EmpleadoId;
        solicitud.FechaResolucionUtc = now;
        solicitud.ComentarioResolucion = NormalizeNullable(request.ComentarioResolucion);
        solicitud.VacacionMovimientoId = movimiento.Id;
        solicitud.IncidenciaId = incidencia.Id;
        solicitud.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return await GetSolicitudByIdAsync(id, actor, cancellationToken);
    }

    public async Task<VacacionesSolicitudDto> RechazarSolicitudAsync(
        int id,
        VacacionesSolicitudResolverDto request,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default)
    {
        if (!CanResolve(actor))
            throw new UnauthorizedAccessException("No tienes permiso para rechazar solicitudes.");

        var solicitud = await LoadSolicitudForMutationAsync(id, cancellationToken);

        if (solicitud.Estatus != EstatusVacacionSolicitud.PENDIENTE)
            throw new InvalidOperationException("Solo se pueden rechazar solicitudes pendientes.");

        if (!CanResolveSolicitud(actor, solicitud))
            throw new UnauthorizedAccessException("No tienes permiso para rechazar esta solicitud.");

        if (string.IsNullOrWhiteSpace(request.MotivoRechazo) &&
            string.IsNullOrWhiteSpace(request.ComentarioResolucion))
        {
            throw new InvalidOperationException("Debes indicar un motivo o comentario de rechazo.");
        }

        var now = DateTime.UtcNow;

        solicitud.Estatus = EstatusVacacionSolicitud.RECHAZADA;
        solicitud.ResueltaPorUsuarioId = actor.UsuarioId;
        solicitud.AprobadorEmpleadoId = actor.EmpleadoId;
        solicitud.FechaResolucionUtc = now;
        solicitud.MotivoRechazo = NormalizeNullable(request.MotivoRechazo);
        solicitud.ComentarioResolucion = NormalizeNullable(request.ComentarioResolucion);
        solicitud.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

                await _notificationService.NotificarSolicitudResueltaAsync(
            id,
            EstatusVacacionSolicitud.RECHAZADA.ToString(),
            cancellationToken);

        return await GetSolicitudByIdAsync(id, actor, cancellationToken);
    }

    public async Task<VacacionesSolicitudDto> CancelarSolicitudAsync(
        int id,
        VacacionesSolicitudResolverDto request,
        VacacionesSolicitudActorDto actor,
        CancellationToken cancellationToken = default)
    {
        var solicitud = await LoadSolicitudForMutationAsync(id, cancellationToken);

        if (solicitud.Estatus != EstatusVacacionSolicitud.PENDIENTE)
            throw new InvalidOperationException("Solo se pueden cancelar solicitudes pendientes.");

        var role = NormalizeRole(actor.Role);

        var canCancel =
            IsAdminOrRrhh(role) ||
            solicitud.SolicitadaPorUsuarioId == actor.UsuarioId ||
            (actor.EmpleadoId.HasValue && solicitud.EmpleadoId == actor.EmpleadoId.Value);

        if (!canCancel)
            throw new UnauthorizedAccessException("No tienes permiso para cancelar esta solicitud.");

        var now = DateTime.UtcNow;

        solicitud.Estatus = EstatusVacacionSolicitud.CANCELADA;
        solicitud.ResueltaPorUsuarioId = actor.UsuarioId;
        solicitud.FechaResolucionUtc = now;
        solicitud.ComentarioResolucion = NormalizeNullable(request.ComentarioResolucion);
        solicitud.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);

                await _notificationService.NotificarSolicitudResueltaAsync(
            id,
            EstatusVacacionSolicitud.CANCELADA.ToString(),
            cancellationToken);

        return await GetSolicitudByIdAsync(id, actor, cancellationToken);
    }

    private IQueryable<VacacionSolicitud> BuildVisibleQuery(VacacionesSolicitudActorDto actor)
    {
        var role = NormalizeRole(actor.Role);

        var query = _db.VacacionSolicitudes
            .AsNoTracking()
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Sucursal)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Departamento)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Puesto)
            .Include(x => x.SolicitadaPorUsuario)
            .Include(x => x.ResueltaPorUsuario)
            .Include(x => x.AprobadorEmpleado)
            .AsQueryable();

        if (IsAdminOrRrhh(role))
            return query;

        if (role == UserRoles.Jefe && actor.EmpleadoId.HasValue)
        {
            var jefeEmpleadoId = actor.EmpleadoId.Value;

            return query.Where(x =>
                x.EmpleadoId == jefeEmpleadoId ||
                (x.Empleado != null &&
                 (x.Empleado.AprobadorPrimarioEmpleadoId == jefeEmpleadoId ||
                  x.Empleado.AprobadorSecundarioEmpleadoId == jefeEmpleadoId)));
        }

        if (actor.EmpleadoId.HasValue)
            return query.Where(x => x.EmpleadoId == actor.EmpleadoId.Value);

        return query.Where(x => false);
    }

    private async Task<VacacionSolicitud> LoadSolicitudForMutationAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var solicitud = await _db.VacacionSolicitudes
            .Include(x => x.Empleado)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return solicitud ?? throw new KeyNotFoundException("La solicitud de vacaciones no existe.");
    }

    private async Task<VacacionPeriodo> ResolvePeriodoAsync(
        int empleadoId,
        int? vacacionPeriodoId,
        decimal diasSolicitados,
        CancellationToken cancellationToken)
    {
        IQueryable<VacacionPeriodo> query = _db.VacacionPeriodos
            .Where(x =>
                x.EmpleadoId == empleadoId &&
                x.Estatus == EstatusVacacionPeriodo.ABIERTO &&
                x.Saldo >= diasSolicitados);

        if (vacacionPeriodoId.HasValue)
            query = query.Where(x => x.Id == vacacionPeriodoId.Value);

        var periodo = await query
            .OrderBy(x => x.FechaLimiteDisfrute)
            .ThenBy(x => x.AnioServicio)
            .FirstOrDefaultAsync(cancellationToken);

        if (periodo is null)
            throw new InvalidOperationException("No se encontró un periodo abierto con saldo suficiente.");

        return periodo;
    }

    private static void ValidateCreateRequest(VacacionesSolicitudCreateDto request)
    {
        if (request.FechaFin < request.FechaInicio)
            throw new InvalidOperationException("La fecha final no puede ser menor a la fecha inicial.");

        if (request.DiasSolicitados <= 0)
            throw new InvalidOperationException("Los días solicitados deben ser mayores a cero.");

        var diasCalendario = request.FechaFin.DayNumber - request.FechaInicio.DayNumber + 1;

        if (request.DiasSolicitados > diasCalendario)
            throw new InvalidOperationException("Los días solicitados no pueden ser mayores al rango de fechas.");
    }

    private static bool CanCreateForEmployee(VacacionesSolicitudActorDto actor, Empleado empleado)
    {
        var role = NormalizeRole(actor.Role);

        if (IsAdminOrRrhh(role))
            return true;

        if (role == UserRoles.Jefe && actor.EmpleadoId.HasValue)
        {
            var jefeEmpleadoId = actor.EmpleadoId.Value;

            return empleado.Id == jefeEmpleadoId ||
                   empleado.AprobadorPrimarioEmpleadoId == jefeEmpleadoId ||
                   empleado.AprobadorSecundarioEmpleadoId == jefeEmpleadoId;
        }

        return actor.EmpleadoId.HasValue && empleado.Id == actor.EmpleadoId.Value;
    }

    private static bool CanResolve(VacacionesSolicitudActorDto actor)
    {
        var role = NormalizeRole(actor.Role);
        return IsAdminOrRrhh(role) || role == UserRoles.Jefe;
    }

    private static bool CanResolveSolicitud(
        VacacionesSolicitudActorDto actor,
        VacacionSolicitud solicitud)
    {
        var role = NormalizeRole(actor.Role);

        if (IsAdminOrRrhh(role))
            return true;

        if (role != UserRoles.Jefe || !actor.EmpleadoId.HasValue || solicitud.Empleado is null)
            return false;

        var jefeEmpleadoId = actor.EmpleadoId.Value;

        return solicitud.Empleado.AprobadorPrimarioEmpleadoId == jefeEmpleadoId ||
               solicitud.Empleado.AprobadorSecundarioEmpleadoId == jefeEmpleadoId;
    }

    private static bool IsAdminOrRrhh(string role)
        => role == UserRoles.Admin || role == UserRoles.Rrhh;

    private static bool IsEmpleadoOnly(string role)
        => role == UserRoles.Empleado || role == UserRoles.Consulta || string.IsNullOrWhiteSpace(role);

    private static string NormalizeRole(string? role)
        => UserRoles.Normalize(role);

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildMovimientoComentario(
        VacacionSolicitud solicitud,
        VacacionesSolicitudResolverDto request)
    {
        var parts = new List<string>
        {
            $"Disfrute aprobado desde solicitud SOL-VAC-{solicitud.Id}.",
            $"Periodo: {solicitud.FechaInicio:yyyy-MM-dd} a {solicitud.FechaFin:yyyy-MM-dd}.",
            $"Días: {solicitud.DiasSolicitados:0.##}."
        };

        if (!string.IsNullOrWhiteSpace(solicitud.ComentarioEmpleado))
            parts.Add($"Comentario empleado: {solicitud.ComentarioEmpleado.Trim()}");

        if (!string.IsNullOrWhiteSpace(request.ComentarioResolucion))
            parts.Add($"Comentario resolución: {request.ComentarioResolucion.Trim()}");

        return string.Join(" ", parts);
    }

    private static VacacionesSolicitudDto ToDtoProjection(VacacionSolicitud x)
    {
        var empleado = x.Empleado;

        return new VacacionesSolicitudDto
        {
            Id = x.Id,

            EmpleadoId = x.EmpleadoId,
            NumEmpleado = empleado != null ? empleado.NumEmpleado : string.Empty,
            NombreEmpleado = empleado != null ? BuildNombreEmpleado(empleado) : string.Empty,

            Sucursal = empleado != null ? empleado.Sucursal != null ? empleado.Sucursal.Nombre : null : null,
            Departamento = empleado != null ? empleado.Departamento != null ? empleado.Departamento.Nombre : null : null,
            Puesto = empleado != null ? empleado.Puesto != null ? empleado.Puesto.Nombre : null : null,

            VacacionPeriodoId = x.VacacionPeriodoId,
            VacacionMovimientoId = x.VacacionMovimientoId,
            IncidenciaId = x.IncidenciaId,

            FechaInicio = x.FechaInicio,
            FechaFin = x.FechaFin,
            DiasSolicitados = x.DiasSolicitados,

            Estatus = x.Estatus,
            EstatusNombre = x.Estatus.ToString(),

            ComentarioEmpleado = x.ComentarioEmpleado,
            ComentarioResolucion = x.ComentarioResolucion,
            MotivoRechazo = x.MotivoRechazo,

            SolicitadaPorUsuarioId = x.SolicitadaPorUsuarioId,
            SolicitadaPorUsuario = x.SolicitadaPorUsuario != null
                ? x.SolicitadaPorUsuario.Email
                : null,

            ResueltaPorUsuarioId = x.ResueltaPorUsuarioId,
            ResueltaPorUsuario = x.ResueltaPorUsuario != null
                ? x.ResueltaPorUsuario.Email
                : null,

            AprobadorEmpleadoId = x.AprobadorEmpleadoId,
            AprobadorEmpleado = x.AprobadorEmpleado != null
                ? BuildNombreEmpleado(x.AprobadorEmpleado)
                : null,

            FechaResolucionUtc = x.FechaResolucionUtc,

            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        };
    }

    private static string BuildNombreEmpleado(Empleado empleado)
    {
        var parts = new[]
        {
            empleado.Nombres?.Trim(),
            empleado.ApellidoPaterno?.Trim(),
            empleado.ApellidoMaterno?.Trim()
        }.Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(" ", parts);
    }
}