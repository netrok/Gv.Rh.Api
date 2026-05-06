using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Options;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;

namespace Gv.Rh.Infrastructure.Services;

public sealed class VacacionesSolicitudesNotificationService
    : IVacacionesSolicitudesNotificationService
{
    private readonly RhDbContext _db;
    private readonly INotificationOutboxService _outbox;
    private readonly NotificationsOptions _options;
    private readonly ILogger<VacacionesSolicitudesNotificationService> _logger;

    public VacacionesSolicitudesNotificationService(
        RhDbContext db,
        INotificationOutboxService outbox,
        IOptions<NotificationsOptions> options,
        ILogger<VacacionesSolicitudesNotificationService> logger)
    {
        _db = db;
        _outbox = outbox;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotificarSolicitudCreadaAsync(
        int solicitudId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        try
        {
            var solicitud = await LoadSolicitudAsync(solicitudId, cancellationToken);

            if (solicitud is null)
            {
                _logger.LogWarning(
                    "No se encontró solicitud {SolicitudId} para encolar notificación de creación.",
                    solicitudId);

                return;
            }

            var recipients = await BuildRecipientsForCreatedAsync(solicitud, cancellationToken);

            await _outbox.QueueEmailAsync(
                recipients,
                $"Nueva solicitud de vacaciones SOL-VAC-{solicitud.Id}",
                BuildCreatedHtml(solicitud),
                tipo: "VACACIONES_SOLICITUD_CREADA",
                correlationType: "VacacionSolicitud",
                correlationId: solicitud.Id,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudo encolar notificación de solicitud creada {SolicitudId}. La solicitud NO se revierte.",
                solicitudId);
        }
    }

    public async Task NotificarSolicitudResueltaAsync(
        int solicitudId,
        string estatus,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        try
        {
            var solicitud = await LoadSolicitudAsync(solicitudId, cancellationToken);

            if (solicitud is null)
            {
                _logger.LogWarning(
                    "No se encontró solicitud {SolicitudId} para encolar notificación de resolución.",
                    solicitudId);

                return;
            }

            var estatusLabel = GetEstatusLabel(estatus);
            var recipients = await BuildRecipientsForResolvedAsync(solicitud, cancellationToken);

            await _outbox.QueueEmailAsync(
                recipients,
                $"Solicitud de vacaciones SOL-VAC-{solicitud.Id} {estatusLabel}",
                BuildResolvedHtml(solicitud, estatusLabel),
                tipo: $"VACACIONES_SOLICITUD_{estatus.ToUpperInvariant()}",
                correlationType: "VacacionSolicitud",
                correlationId: solicitud.Id,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudo encolar notificación de solicitud resuelta {SolicitudId}. La operación principal NO se revierte.",
                solicitudId);
        }
    }

    private async Task<VacacionSolicitud?> LoadSolicitudAsync(
        int solicitudId,
        CancellationToken cancellationToken)
    {
        return await _db.VacacionSolicitudes
            .AsNoTracking()
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Sucursal)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Departamento)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Puesto)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.AprobadorPrimario)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.AprobadorSecundario)
            .Include(x => x.SolicitadaPorUsuario)
            .Include(x => x.ResueltaPorUsuario)
            .Include(x => x.AprobadorEmpleado)
            .FirstOrDefaultAsync(x => x.Id == solicitudId, cancellationToken);
    }

    private async Task<List<string>> BuildRecipientsForCreatedAsync(
        VacacionSolicitud solicitud,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddMany(recipients, _options.VacacionesRecipients);

        Add(recipients, solicitud.Empleado?.AprobadorPrimario?.Email);
        Add(recipients, solicitud.Empleado?.AprobadorSecundario?.Email);

        var aprobadorIds = new List<int>();

        if (solicitud.Empleado?.AprobadorPrimarioEmpleadoId is int primarioId)
            aprobadorIds.Add(primarioId);

        if (solicitud.Empleado?.AprobadorSecundarioEmpleadoId is int secundarioId)
            aprobadorIds.Add(secundarioId);

        if (aprobadorIds.Count > 0)
        {
            var usuariosAprobadores = await _db.Users
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.EmpleadoId.HasValue &&
                    aprobadorIds.Contains(x.EmpleadoId.Value))
                .Select(x => x.Email)
                .ToListAsync(cancellationToken);

            AddMany(recipients, usuariosAprobadores);
        }

        if (recipients.Count == 0)
            AddMany(recipients, _options.ExpedienteRecipients);

        return recipients.ToList();
    }

    private async Task<List<string>> BuildRecipientsForResolvedAsync(
        VacacionSolicitud solicitud,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Add(recipients, solicitud.Empleado?.Email);
        Add(recipients, solicitud.SolicitadaPorUsuario?.Email);

        var usuariosEmpleado = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.EmpleadoId == solicitud.EmpleadoId)
            .Select(x => x.Email)
            .ToListAsync(cancellationToken);

        AddMany(recipients, usuariosEmpleado);

        return recipients.ToList();
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

    private static string BuildCreatedHtml(VacacionSolicitud solicitud)
    {
        var empleado = solicitud.Empleado;
        var nombre = BuildNombreEmpleado(empleado);

        return BuildLayout(
            title: "Nueva solicitud de vacaciones",
            intro: "Se registró una solicitud de vacaciones pendiente de revisión.",
            body: $"""
            <table style="width:100%;border-collapse:collapse;margin-top:16px;">
              {Row("Folio", $"SOL-VAC-{solicitud.Id}")}
              {Row("Empleado", $"{Html(nombre)} ({Html(empleado?.NumEmpleado)})")}
              {Row("Periodo solicitado", $"{FormatDate(solicitud.FechaInicio)} al {FormatDate(solicitud.FechaFin)}")}
              {Row("Días solicitados", $"{solicitud.DiasSolicitados:0.##}")}
              {Row("Sucursal", Html(empleado?.Sucursal?.Nombre))}
              {Row("Departamento", Html(empleado?.Departamento?.Nombre))}
              {Row("Puesto", Html(empleado?.Puesto?.Nombre))}
              {Row("Comentario", Html(solicitud.ComentarioEmpleado))}
            </table>
            <p style="margin-top:18px;color:#475569;">
              Ingresa a GRANVIA RH para revisar, aprobar o rechazar la solicitud.
            </p>
            """);
    }

    private static string BuildResolvedHtml(
        VacacionSolicitud solicitud,
        string estatusLabel)
    {
        var empleado = solicitud.Empleado;
        var nombre = BuildNombreEmpleado(empleado);

        return BuildLayout(
            title: $"Solicitud de vacaciones {Html(estatusLabel)}",
            intro: $"Tu solicitud de vacaciones SOL-VAC-{solicitud.Id} fue marcada como {Html(estatusLabel)}.",
            body: $"""
            <table style="width:100%;border-collapse:collapse;margin-top:16px;">
              {Row("Folio", $"SOL-VAC-{solicitud.Id}")}
              {Row("Empleado", $"{Html(nombre)} ({Html(empleado?.NumEmpleado)})")}
              {Row("Periodo solicitado", $"{FormatDate(solicitud.FechaInicio)} al {FormatDate(solicitud.FechaFin)}")}
              {Row("Días solicitados", $"{solicitud.DiasSolicitados:0.##}")}
              {Row("Estatus", Html(estatusLabel))}
              {Row("Resuelto por", Html(solicitud.ResueltaPorUsuario?.Email ?? BuildNombreEmpleado(solicitud.AprobadorEmpleado)))}
              {Row("Fecha resolución", solicitud.FechaResolucionUtc.HasValue ? solicitud.FechaResolucionUtc.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "—")}
              {Row("Motivo rechazo", Html(solicitud.MotivoRechazo))}
              {Row("Comentario resolución", Html(solicitud.ComentarioResolucion))}
            </table>
            <p style="margin-top:18px;color:#475569;">
              Puedes consultar el detalle en GRANVIA RH.
            </p>
            """);
    }

    private static string BuildLayout(string title, string intro, string body)
    {
        return $"""
        <!doctype html>
        <html>
        <body style="margin:0;padding:0;background:#f8fafc;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
          <div style="max-width:720px;margin:0 auto;padding:24px;">
            <div style="background:#ffffff;border:1px solid #e2e8f0;border-radius:18px;overflow:hidden;">
              <div style="background:#0f172a;color:#ffffff;padding:20px 24px;">
                <div style="font-size:12px;letter-spacing:.12em;text-transform:uppercase;color:#cbd5e1;">
                  GRANVIA Recursos Humanos
                </div>
                <h1 style="font-size:22px;line-height:1.25;margin:8px 0 0 0;">
                  {Html(title)}
                </h1>
              </div>
              <div style="padding:22px 24px;">
                <p style="font-size:15px;line-height:1.6;margin:0;color:#334155;">
                  {Html(intro)}
                </p>
                {body}
                <div style="margin-top:24px;padding-top:16px;border-top:1px solid #e2e8f0;color:#64748b;font-size:12px;">
                  Este correo fue generado automáticamente por GRANVIA RH.
                </div>
              </div>
            </div>
          </div>
        </body>
        </html>
        """;
    }

    private static string Row(string label, string? value)
    {
        var safeValue = string.IsNullOrWhiteSpace(value) ? "—" : value;

        return $"""
        <tr>
          <td style="width:190px;padding:10px 12px;border-bottom:1px solid #e2e8f0;background:#f8fafc;color:#64748b;font-size:13px;font-weight:700;">
            {Html(label)}
          </td>
          <td style="padding:10px 12px;border-bottom:1px solid #e2e8f0;color:#0f172a;font-size:13px;">
            {safeValue}
          </td>
        </tr>
        """;
    }

    private static string FormatDate(DateOnly value)
        => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Html(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string BuildNombreEmpleado(Empleado? empleado)
    {
        if (empleado is null)
            return "Empleado";

        return string.Join(" ", new[]
        {
            empleado.Nombres?.Trim(),
            empleado.ApellidoPaterno?.Trim(),
            empleado.ApellidoMaterno?.Trim()
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string GetEstatusLabel(string estatus)
    {
        var raw = (estatus ?? string.Empty).Trim().ToUpperInvariant();

        return raw switch
        {
            "APROBADA" => "aprobada",
            "RECHAZADA" => "rechazada",
            "CANCELADA" => "cancelada",
            _ => raw.ToLowerInvariant()
        };
    }
}