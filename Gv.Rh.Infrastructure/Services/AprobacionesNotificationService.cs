using System.Globalization;
using System.Net;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Common.Enums;
using Gv.Rh.Infrastructure.Options;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gv.Rh.Infrastructure.Services;

public sealed class AprobacionesNotificationService : IAprobacionesNotificationService
{
    private const string NotificationType = "APROBACIONES_PENDIENTES_DIARIAS";
    private const int MaxDetalleItems = 12;

    private readonly RhDbContext _db;
    private readonly INotificationOutboxService _outbox;
    private readonly NotificationsOptions _options;
    private readonly ILogger<AprobacionesNotificationService> _logger;

    public AprobacionesNotificationService(
        RhDbContext db,
        INotificationOutboxService outbox,
        IOptions<NotificationsOptions> options,
        ILogger<AprobacionesNotificationService> logger)
    {
        _db = db;
        _outbox = outbox;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> NotificarResumenPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Notificaciones deshabilitadas. No se encoló resumen de aprobaciones.");
            return 0;
        }

        var todayKey = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var correlationType = $"{NotificationType}:{todayKey}";

        var jefeUsersRaw = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.EmpleadoId.HasValue &&
                (x.Role ?? string.Empty).ToUpper() == UserRoles.Jefe)
            .Select(x => new
            {
                x.Email,
                EmpleadoId = x.EmpleadoId!.Value
            })
            .ToListAsync(cancellationToken);

        var jefeGroups = jefeUsersRaw
            .GroupBy(x => x.EmpleadoId)
            .Select(x => new
            {
                EmpleadoId = x.Key,
                Emails = x
                    .Select(y => y.Email)
                    .Where(email => !string.IsNullOrWhiteSpace(email) && email.Contains('@'))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();

        if (jefeGroups.Count == 0)
        {
            _logger.LogInformation("No se encontraron usuarios JEFE activos con empleado ligado.");
            return 0;
        }

        var jefeEmpleadoIds = jefeGroups
            .Select(x => x.EmpleadoId)
            .Distinct()
            .ToList();

        var jefes = await _db.Empleados
            .AsNoTracking()
            .Where(x => jefeEmpleadoIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.NumEmpleado,
                x.Nombres,
                x.ApellidoPaterno,
                x.ApellidoMaterno,
                x.Email
            })
            .ToListAsync(cancellationToken);

        var creadas = 0;

        foreach (var group in jefeGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jefeEmpleadoId = group.EmpleadoId;
            var jefe = jefes.FirstOrDefault(x => x.Id == jefeEmpleadoId);

            if (jefe is null)
            {
                _logger.LogWarning(
                    "Usuario JEFE ligado a empleado inexistente. EmpleadoId={EmpleadoId}",
                    jefeEmpleadoId);

                continue;
            }

            var yaExisteHoy = await _db.NotificacionesOutbox
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Tipo == NotificationType &&
                    x.CorrelationType == correlationType &&
                    x.CorrelationId == jefeEmpleadoId,
                    cancellationToken);

            if (yaExisteHoy)
            {
                _logger.LogInformation(
                    "Resumen diario de aprobaciones ya existe para jefe {JefeEmpleadoId}. Correlation={CorrelationType}",
                    jefeEmpleadoId,
                    correlationType);

                continue;
            }

            var incidenciasQuery = _db.Incidencias
                .AsNoTracking()
                .Where(x =>
                    x.Estatus == EstatusIncidencia.PENDIENTE &&
                    (
                        x.Empleado.AprobadorPrimarioEmpleadoId == jefeEmpleadoId ||
                        x.Empleado.AprobadorSecundarioEmpleadoId == jefeEmpleadoId
                    ));

            var vacacionesQuery = _db.VacacionSolicitudes
                .AsNoTracking()
                .Where(x =>
                    x.Estatus == EstatusVacacionSolicitud.PENDIENTE &&
                    x.Empleado != null &&
                    (
                        x.Empleado.AprobadorPrimarioEmpleadoId == jefeEmpleadoId ||
                        x.Empleado.AprobadorSecundarioEmpleadoId == jefeEmpleadoId
                    ));

            var incidenciasPendientes = await incidenciasQuery.CountAsync(cancellationToken);
            var vacacionesPendientes = await vacacionesQuery.CountAsync(cancellationToken);
            var totalPendientes = incidenciasPendientes + vacacionesPendientes;

            if (totalPendientes <= 0)
            {
                continue;
            }

            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddMany(recipients, group.Emails);
            Add(recipients, jefe.Email);

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "No se encoló resumen de aprobaciones para jefe {JefeEmpleadoId}; no tiene correo válido.",
                    jefeEmpleadoId);

                continue;
            }

            var incidenciaRows = await incidenciasQuery
                .OrderBy(x => x.FechaInicio)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.FechaInicio,
                    x.FechaFin,
                    x.Tipo,
                    x.Empleado.NumEmpleado,
                    x.Empleado.Nombres,
                    x.Empleado.ApellidoPaterno,
                    x.Empleado.ApellidoMaterno
                })
                .Take(MaxDetalleItems)
                .ToListAsync(cancellationToken);

            var vacacionesRows = await vacacionesQuery
                .OrderBy(x => x.FechaInicio)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.FechaInicio,
                    x.FechaFin,
                    x.DiasSolicitados,
                    NumEmpleado = x.Empleado != null ? x.Empleado.NumEmpleado : string.Empty,
                    Nombres = x.Empleado != null ? x.Empleado.Nombres : string.Empty,
                    ApellidoPaterno = x.Empleado != null ? x.Empleado.ApellidoPaterno : string.Empty,
                    ApellidoMaterno = x.Empleado != null ? x.Empleado.ApellidoMaterno : null
                })
                .Take(MaxDetalleItems)
                .ToListAsync(cancellationToken);

            var items = new List<AprobacionResumenItem>();

            items.AddRange(incidenciaRows.Select(x => new AprobacionResumenItem
            {
                Tipo = "Incidencia",
                Folio = $"INC-{x.Id}",
                Empleado = BuildNombreEmpleado(x.Nombres, x.ApellidoPaterno, x.ApellidoMaterno),
                NumEmpleado = x.NumEmpleado,
                Periodo = $"{FormatDate(x.FechaInicio)} al {FormatDate(x.FechaFin)}",
                Descripcion = FormatLabel(x.Tipo.ToString()),
                FechaOrden = x.FechaInicio
            }));

            items.AddRange(vacacionesRows.Select(x => new AprobacionResumenItem
            {
                Tipo = "Vacaciones",
                Folio = $"SOL-VAC-{x.Id}",
                Empleado = BuildNombreEmpleado(x.Nombres, x.ApellidoPaterno, x.ApellidoMaterno),
                NumEmpleado = x.NumEmpleado,
                Periodo = $"{FormatDate(x.FechaInicio)} al {FormatDate(x.FechaFin)}",
                Descripcion = $"{x.DiasSolicitados:0.##} día(s)",
                FechaOrden = x.FechaInicio
            }));

            items = items
                .OrderBy(x => x.FechaOrden)
                .ThenBy(x => x.Tipo)
                .ThenBy(x => x.Folio)
                .Take(MaxDetalleItems)
                .ToList();

            var jefeNombre = BuildNombreEmpleado(jefe.Nombres, jefe.ApellidoPaterno, jefe.ApellidoMaterno);
            var subject = $"Tienes {totalPendientes} aprobación(es) pendiente(s) en GRANVIA RH";

            var html = BuildHtml(
                jefeNombre,
                incidenciasPendientes,
                vacacionesPendientes,
                totalPendientes,
                items);

            var outboxId = await _outbox.QueueEmailAsync(
                recipients,
                subject,
                html,
                tipo: NotificationType,
                correlationType: correlationType,
                correlationId: jefeEmpleadoId,
                cancellationToken: cancellationToken);

            if (outboxId.HasValue)
            {
                creadas++;
            }
        }

        _logger.LogInformation(
            "Resumen de aprobaciones pendientes encolado. NotificacionesCreadas={Creadas}",
            creadas);

        return creadas;
    }

    private static string BuildHtml(
        string jefeNombre,
        int incidenciasPendientes,
        int vacacionesPendientes,
        int totalPendientes,
        IReadOnlyList<AprobacionResumenItem> items)
    {
        var rows = items.Count == 0
            ? """
              <tr>
                <td colspan="5" style="padding:14px 12px;border-bottom:1px solid #e2e8f0;color:#64748b;font-size:13px;">
                  No hay detalle disponible.
                </td>
              </tr>
              """
            : string.Join(Environment.NewLine, items.Select(BuildRow));

        return $"""
        <!doctype html>
        <html>
        <body style="margin:0;padding:0;background:#f8fafc;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
          <div style="max-width:760px;margin:0 auto;padding:24px;">
            <div style="background:#ffffff;border:1px solid #e2e8f0;border-radius:18px;overflow:hidden;">
              <div style="background:#0f172a;color:#ffffff;padding:22px 24px;">
                <div style="font-size:12px;letter-spacing:.12em;text-transform:uppercase;color:#cbd5e1;">
                  GRANVIA Recursos Humanos
                </div>
                <h1 style="font-size:22px;line-height:1.25;margin:8px 0 0 0;">
                  Resumen diario de aprobaciones pendientes
                </h1>
              </div>

              <div style="padding:22px 24px;">
                <p style="font-size:15px;line-height:1.6;margin:0;color:#334155;">
                  Hola {Html(jefeNombre)}, tienes aprobaciones pendientes por revisar en GRANVIA RH.
                </p>

                <div style="display:flex;gap:12px;margin-top:18px;flex-wrap:wrap;">
                  {Kpi("Total pendientes", totalPendientes.ToString(CultureInfo.InvariantCulture))}
                  {Kpi("Incidencias", incidenciasPendientes.ToString(CultureInfo.InvariantCulture))}
                  {Kpi("Vacaciones", vacacionesPendientes.ToString(CultureInfo.InvariantCulture))}
                </div>

                <table style="width:100%;border-collapse:collapse;margin-top:20px;">
                  <thead>
                    <tr>
                      {Header("Tipo")}
                      {Header("Folio")}
                      {Header("Empleado")}
                      {Header("Periodo")}
                      {Header("Detalle")}
                    </tr>
                  </thead>
                  <tbody>
                    {rows}
                  </tbody>
                </table>

                <p style="margin-top:18px;color:#475569;font-size:14px;line-height:1.6;">
                  Ingresa a GRANVIA RH y abre el módulo <strong>Mis aprobaciones</strong> para revisar, aprobar o rechazar los pendientes.
                </p>

                <div style="margin-top:24px;padding-top:16px;border-top:1px solid #e2e8f0;color:#64748b;font-size:12px;">
                  Este correo fue generado automáticamente por GRANVIA RH. Se envía máximo una vez al día por jefe.
                </div>
              </div>
            </div>
          </div>
        </body>
        </html>
        """;
    }

    private static string Kpi(string label, string value)
    {
        return $"""
        <div style="min-width:145px;padding:12px 14px;border:1px solid #e2e8f0;border-radius:14px;background:#f8fafc;">
          <div style="font-size:12px;color:#64748b;font-weight:700;">{Html(label)}</div>
          <div style="font-size:26px;color:#0f172a;font-weight:800;line-height:1.1;margin-top:4px;">{Html(value)}</div>
        </div>
        """;
    }

    private static string Header(string label)
    {
        return $"""
        <th style="padding:10px 12px;border-bottom:1px solid #cbd5e1;background:#f1f5f9;color:#334155;font-size:12px;text-align:left;">
          {Html(label)}
        </th>
        """;
    }

    private static string BuildRow(AprobacionResumenItem item)
    {
        return $"""
        <tr>
          <td style="padding:10px 12px;border-bottom:1px solid #e2e8f0;color:#0f172a;font-size:13px;font-weight:700;">{Html(item.Tipo)}</td>
          <td style="padding:10px 12px;border-bottom:1px solid #e2e8f0;color:#0f172a;font-size:13px;">{Html(item.Folio)}</td>
          <td style="padding:10px 12px;border-bottom:1px solid #e2e8f0;color:#0f172a;font-size:13px;">{Html(item.Empleado)} <span style="color:#64748b;">#{Html(item.NumEmpleado)}</span></td>
          <td style="padding:10px 12px;border-bottom:1px solid #e2e8f0;color:#0f172a;font-size:13px;">{Html(item.Periodo)}</td>
          <td style="padding:10px 12px;border-bottom:1px solid #e2e8f0;color:#0f172a;font-size:13px;">{Html(item.Descripcion)}</td>
        </tr>
        """;
    }

    private static void AddMany(HashSet<string> recipients, IEnumerable<string>? emails)
    {
        if (emails is null)
            return;

        foreach (var email in emails)
            Add(recipients, email);
    }

    private static void Add(HashSet<string> recipients, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        var value = email.Trim();

        if (!value.Contains('@'))
            return;

        recipients.Add(value);
    }

    private static string BuildNombreEmpleado(
        string? nombres,
        string? apellidoPaterno,
        string? apellidoMaterno)
    {
        return string.Join(" ", new[]
        {
            nombres?.Trim(),
            apellidoPaterno?.Trim(),
            apellidoMaterno?.Trim()
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string FormatDate(DateOnly value)
        => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string FormatLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Replace("_", " ").ToLowerInvariant();
    }

    private static string Html(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private sealed class AprobacionResumenItem
    {
        public string Tipo { get; set; } = string.Empty;
        public string Folio { get; set; } = string.Empty;
        public string Empleado { get; set; } = string.Empty;
        public string NumEmpleado { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateOnly FechaOrden { get; set; }
    }
}
