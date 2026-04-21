using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Gv.Rh.Application.DTOs.EmpleadosDocumentos;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Options;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gv.Rh.Infrastructure.Services;

public sealed class ExpedienteNotificationService : IExpedienteNotificationService
{
    private readonly RhDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly NotificationsOptions _options;
    private readonly ILogger<ExpedienteNotificationService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExpedienteNotificationService(
        RhDbContext dbContext,
        IEmailService emailService,
        IOptions<NotificationsOptions> options,
        ILogger<ExpedienteNotificationService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _options = options.Value;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<DocumentoVencimientoResumenDto> ObtenerResumenAsync(
        int diasAnticipacion,
        bool incluirVencidos,
        CancellationToken cancellationToken = default)
    {
        diasAnticipacion = NormalizarDiasAnticipacion(diasAnticipacion);

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var documentos = await _dbContext.EmpleadoDocumentos
            .AsNoTracking()
            .Include(x => x.Empleado)
                .ThenInclude(e => e!.Sucursal)
            .Where(x =>
                x.Activo &&
                x.FechaVencimiento != null &&
                x.Empleado != null &&
                x.Empleado.Activo)
            .ToListAsync(cancellationToken);

        var items = documentos
            .Select(x =>
            {
                var fechaVencimiento = x.FechaVencimiento!.Value;
                var diasParaVencer = fechaVencimiento.DayNumber - hoy.DayNumber;
                var empleado = x.Empleado!;

                return new DocumentoVencimientoItemDto
                {
                    EmpleadoId = x.EmpleadoId,
                    NumEmpleado = empleado.NumEmpleado ?? string.Empty,
                    EmpleadoNombreCompleto = BuildNombreCompleto(
                        empleado.Nombres,
                        empleado.ApellidoPaterno,
                        empleado.ApellidoMaterno),
                    TipoDocumento = x.Tipo.ToString(),
                    FechaDocumento = x.FechaDocumento,
                    FechaVencimiento = x.FechaVencimiento,
                    DiasParaVencer = diasParaVencer,
                    EstaVencido = diasParaVencer < 0,
                    Comentario = x.Comentario
                };
            })
            .ToList();

        var porVencer = items
            .Where(x => !x.EstaVencido && x.DiasParaVencer <= diasAnticipacion)
            .OrderBy(x => x.DiasParaVencer)
            .ThenBy(x => x.EmpleadoNombreCompleto)
            .ThenBy(x => x.TipoDocumento)
            .ToList();

        var vencidos = incluirVencidos
            ? items
                .Where(x => x.EstaVencido)
                .OrderBy(x => x.DiasParaVencer)
                .ThenBy(x => x.EmpleadoNombreCompleto)
                .ThenBy(x => x.TipoDocumento)
                .ToList()
            : [];

        return new DocumentoVencimientoResumenDto
        {
            FechaCorte = hoy,
            DiasAnticipacion = diasAnticipacion,
            TotalPorVencer = porVencer.Count,
            TotalVencidos = vencidos.Count,
            PorVencer = porVencer,
            Vencidos = vencidos
        };
    }

    public async Task<int> EnviarResumenAsync(
        int diasAnticipacion,
        bool incluirVencidos,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Notificaciones de expediente deshabilitadas.");
            return 0;
        }

        if (_options.ExpedienteRecipients is null || _options.ExpedienteRecipients.Count == 0)
        {
            _logger.LogWarning("No hay destinatarios configurados en Notifications:ExpedienteRecipients.");
            return 0;
        }

        var resumen = await ObtenerResumenAsync(
            diasAnticipacion,
            incluirVencidos,
            cancellationToken);

        if (resumen.TotalPorVencer == 0 && resumen.TotalVencidos == 0)
        {
            _logger.LogInformation("No hay documentos por vencer ni vencidos para notificar.");
            return 0;
        }

        var subject = $"[GV RH] Documentos por vencer/vencidos - {resumen.FechaCorte:yyyy-MM-dd}";
        var html = BuildHtml(resumen);

        try
        {
            await _emailService.SendAsync(
                _options.ExpedienteRecipients,
                subject: subject,
                htmlBody: html,
                cancellationToken: cancellationToken);

            await RegistrarAuditoriaAsync(
                resumen,
                status: "SUCCESS",
                error: null,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Resumen de expediente enviado. Por vencer: {PorVencer}. Vencidos: {Vencidos}. Destinatarios: {Destinatarios}",
                resumen.TotalPorVencer,
                resumen.TotalVencidos,
                string.Join(", ", _options.ExpedienteRecipients));

            return resumen.TotalPorVencer + resumen.TotalVencidos;
        }
        catch (Exception ex)
        {
            await RegistrarAuditoriaAsync(
                resumen,
                status: "ERROR",
                error: ex.Message,
                cancellationToken: cancellationToken);

            _logger.LogError(ex, "Error enviando resumen de vencimientos de expediente.");
            throw;
        }
    }

    private async Task RegistrarAuditoriaAsync(
        DocumentoVencimientoResumenDto resumen,
        string status,
        string? error,
        CancellationToken cancellationToken)
    {
        var contexto = ObtenerContextoAuditoria();
        var esManual = contexto.HttpContextDisponible;

        var action = status == "SUCCESS"
            ? (esManual ? "EXPEDIENTE_NOTIFY_MANUAL" : "EXPEDIENTE_NOTIFY_SCHEDULER")
            : (esManual ? "EXPEDIENTE_NOTIFY_MANUAL_ERROR" : "EXPEDIENTE_NOTIFY_SCHEDULER_ERROR");

        var payload = new
        {
            origen = esManual ? "MANUAL" : "SCHEDULER",
            status,
            fechaCorte = resumen.FechaCorte.ToString("yyyy-MM-dd"),
            diasAnticipacion = resumen.DiasAnticipacion,
            totalPorVencer = resumen.TotalPorVencer,
            totalVencidos = resumen.TotalVencidos,
            totalNotificados = resumen.TotalPorVencer + resumen.TotalVencidos,
            destinatarios = _options.ExpedienteRecipients,
            error
        };

        var changedColumns = new[]
        {
            "origen",
            "status",
            "fechaCorte",
            "diasAnticipacion",
            "totalPorVencer",
            "totalVencidos",
            "totalNotificados",
            "destinatarios",
            "error"
        };

        var audit = new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            UserId = null,
            UserEmail = contexto.UserEmail,
            UserRole = contexto.UserRole,
            Action = action,
            EntityName = "ExpedienteNotificaciones",
            RecordId = string.Empty,
            IpAddress = contexto.IpAddress ?? string.Empty,
            UserAgent = contexto.UserAgent ?? string.Empty,
            OldValuesJson = "null",
            NewValuesJson = JsonSerializer.Serialize(payload) ?? "{}",
            ChangedColumnsJson = JsonSerializer.Serialize(changedColumns) ?? "[]"
        };

        _dbContext.AuditLogs.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private AuditContext ObtenerContextoAuditoria()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return new AuditContext
            {
                HttpContextDisponible = false,
                UserEmail = "system@gv-rh.local",
                UserRole = "SYSTEM",
                IpAddress = null,
                UserAgent = "BackgroundService/ExpedienteNotificationHostedService"
            };
        }

        var user = httpContext.User;

        return new AuditContext
        {
            HttpContextDisponible = true,
            UserEmail = GetFirstClaimValue(
                user,
                ClaimTypes.Email,
                "email",
                "emails")
                ?? user.Identity?.Name
                ?? "usuario-desconocido@gv-rh.local",
            UserRole = GetFirstClaimValue(
                user,
                ClaimTypes.Role,
                "role",
                "roles",
                "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                ?? "SIN_ROL",
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString()
        };
    }

    private static string? GetFirstClaimValue(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static int NormalizarDiasAnticipacion(int diasAnticipacion)
    {
        if (diasAnticipacion < 1)
            return 1;

        if (diasAnticipacion > 365)
            return 365;

        return diasAnticipacion;
    }

    private static string BuildHtml(DocumentoVencimientoResumenDto resumen)
    {
        var sb = new StringBuilder();

        sb.Append("""
<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="utf-8" />
  <title>Resumen de vencimientos de expediente</title>
  <style>
    body { font-family: Segoe UI, Arial, sans-serif; color: #1f2937; margin: 0; padding: 24px; }
    h1 { font-size: 22px; margin: 0 0 8px 0; color: #0f172a; }
    h2 { font-size: 16px; margin: 24px 0 12px 0; color: #0f172a; }
    p { margin: 0 0 12px 0; }
    .meta { margin-bottom: 16px; color: #475569; }
    .summary { margin: 12px 0 18px 0; }
    .badge { display: inline-block; padding: 4px 10px; border-radius: 999px; font-size: 12px; font-weight: 600; }
    .warn { background: #fef3c7; color: #92400e; }
    .danger { background: #fee2e2; color: #991b1b; }
    table { width: 100%; border-collapse: collapse; margin-top: 10px; }
    th, td { border: 1px solid #e5e7eb; padding: 8px; font-size: 13px; text-align: left; vertical-align: top; }
    th { background: #f8fafc; }
    .muted { color: #64748b; }
  </style>
</head>
<body>
""");

        sb.Append("<h1>Resumen de vencimientos de expediente</h1>");
        sb.Append(
            $"<div class='meta'>Fecha de corte: <strong>{resumen.FechaCorte:yyyy-MM-dd}</strong> | Ventana de revisión: <strong>{resumen.DiasAnticipacion} día(s)</strong></div>");

        sb.Append(
            $"<div class='summary'><strong>Total por vencer:</strong> {resumen.TotalPorVencer} &nbsp;|&nbsp; <strong>Total vencidos:</strong> {resumen.TotalVencidos}</div>");

        if (resumen.PorVencer.Count > 0)
        {
            sb.Append("<h2><span class='badge warn'>Por vencer</span></h2>");
            sb.Append("""
<table>
  <thead>
    <tr>
      <th>No. empleado</th>
      <th>Empleado</th>
      <th>Documento</th>
      <th>Fecha documento</th>
      <th>Vence</th>
      <th>Días restantes</th>
      <th>Comentario</th>
    </tr>
  </thead>
  <tbody>
""");

            foreach (var item in resumen.PorVencer)
            {
                sb.Append($$"""
    <tr>
      <td>{{Encode(item.NumEmpleado)}}</td>
      <td>{{Encode(item.EmpleadoNombreCompleto)}}</td>
      <td>{{Encode(item.TipoDocumento)}}</td>
      <td>{{FormatDate(item.FechaDocumento)}}</td>
      <td>{{FormatDate(item.FechaVencimiento)}}</td>
      <td>{{item.DiasParaVencer}}</td>
      <td>{{Encode(item.Comentario)}}</td>
    </tr>
""");
            }

            sb.Append("""
  </tbody>
</table>
""");
        }

        if (resumen.Vencidos.Count > 0)
        {
            sb.Append("<h2><span class='badge danger'>Vencidos</span></h2>");
            sb.Append("""
<table>
  <thead>
    <tr>
      <th>No. empleado</th>
      <th>Empleado</th>
      <th>Documento</th>
      <th>Fecha documento</th>
      <th>Venció</th>
      <th>Días vencido</th>
      <th>Comentario</th>
    </tr>
  </thead>
  <tbody>
""");

            foreach (var item in resumen.Vencidos)
            {
                sb.Append($$"""
    <tr>
      <td>{{Encode(item.NumEmpleado)}}</td>
      <td>{{Encode(item.EmpleadoNombreCompleto)}}</td>
      <td>{{Encode(item.TipoDocumento)}}</td>
      <td>{{FormatDate(item.FechaDocumento)}}</td>
      <td>{{FormatDate(item.FechaVencimiento)}}</td>
      <td>{{Math.Abs(item.DiasParaVencer)}}</td>
      <td>{{Encode(item.Comentario)}}</td>
    </tr>
""");
            }

            sb.Append("""
  </tbody>
</table>
""");
        }

        if (resumen.PorVencer.Count == 0 && resumen.Vencidos.Count == 0)
        {
            sb.Append("<p class='muted'>No se encontraron documentos por vencer ni vencidos en este corte.</p>");
        }

        sb.Append("""
</body>
</html>
""");

        return sb.ToString();
    }

    private static string BuildNombreCompleto(
        string? nombres,
        string? apellidoPaterno,
        string? apellidoMaterno)
    {
        return string.Join(
            " ",
            new[] { nombres, apellidoPaterno, apellidoMaterno }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
    }

    private static string Encode(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string FormatDate(DateOnly? value)
        => value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "-";

    private sealed class AuditContext
    {
        public bool HttpContextDisponible { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}