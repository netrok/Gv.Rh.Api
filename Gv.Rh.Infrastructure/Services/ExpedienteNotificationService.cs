using Gv.Rh.Application.Interfaces;
using Gv.Rh.Infrastructure.Options;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace Gv.Rh.Infrastructure.Services;

public sealed class ExpedienteNotificationService : IExpedienteNotificationService
{
    private readonly RhDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly NotificationsOptions _options;

    public ExpedienteNotificationService(
        RhDbContext dbContext,
        IEmailService emailService,
        IOptions<NotificationsOptions> options)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _options = options.Value;
    }

    public async Task<int> NotificarDocumentosPorVencerAsync(
        int diasAnticipacion,
        CancellationToken cancellationToken = default)
    {
        if (diasAnticipacion < 1)
        {
            diasAnticipacion = 1;
        }

        if (diasAnticipacion > 365)
        {
            diasAnticipacion = 365;
        }

        if (_options.ExpedienteRecipients is null || _options.ExpedienteRecipients.Count == 0)
        {
            throw new InvalidOperationException(
                "No hay destinatarios configurados en Notifications:ExpedienteRecipients.");
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var limite = hoy.AddDays(diasAnticipacion);

        var documentos = await _dbContext.EmpleadoDocumentos
            .AsNoTracking()
            .Where(d =>
                d.Activo &&
                d.FechaVencimiento != null &&
                d.FechaVencimiento >= hoy &&
                d.FechaVencimiento <= limite &&
                d.Empleado != null &&
                d.Empleado.Activo)
            .Select(d => new DocumentoPorVencerItem
            {
                Id = d.Id,
                EmpleadoId = d.EmpleadoId,
                Tipo = d.Tipo.ToString(),
                FechaVencimiento = d.FechaVencimiento!.Value,
                NumEmpleado = d.Empleado.NumEmpleado,
                Nombres = d.Empleado.Nombres,
                ApellidoPaterno = d.Empleado.ApellidoPaterno,
                ApellidoMaterno = d.Empleado.ApellidoMaterno,
                SucursalNombre = d.Empleado.Sucursal != null
                    ? d.Empleado.Sucursal.Nombre
                    : null
            })
            .OrderBy(d => d.FechaVencimiento)
            .ThenBy(d => d.Nombres)
            .ThenBy(d => d.ApellidoPaterno)
            .ToListAsync(cancellationToken);

        if (documentos.Count == 0)
        {
            return 0;
        }

        var html = BuildHtml(documentos, hoy, diasAnticipacion);

        await _emailService.SendAsync(
            _options.ExpedienteRecipients,
            subject: $"Documentos por vencer - {DateTime.Today:dd/MM/yyyy}",
            htmlBody: html,
            cancellationToken: cancellationToken);

        return documentos.Count;
    }

    private static string BuildHtml(
        IReadOnlyList<DocumentoPorVencerItem> documentos,
        DateOnly hoy,
        int diasAnticipacion)
    {
        var sb = new StringBuilder();

        sb.Append($"""
        <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937">
          <h2 style="margin:0 0 12px 0;color:#0f172a">Documentos próximos a vencer</h2>
          <p style="margin:0 0 16px 0">
            Se encontraron documentos con vencimiento dentro de los próximos {diasAnticipacion} día(s).
          </p>
          <table style="border-collapse:collapse;width:100%">
            <thead>
              <tr>
                <th style="text-align:left;padding:8px;border-bottom:1px solid #e5e7eb">Empleado</th>
                <th style="text-align:left;padding:8px;border-bottom:1px solid #e5e7eb">Documento</th>
                <th style="text-align:left;padding:8px;border-bottom:1px solid #e5e7eb">Vence</th>
                <th style="text-align:left;padding:8px;border-bottom:1px solid #e5e7eb">Días restantes</th>
                <th style="text-align:left;padding:8px;border-bottom:1px solid #e5e7eb">Sucursal</th>
              </tr>
            </thead>
            <tbody>
        """);

        foreach (var item in documentos)
        {
            var diasRestantes = item.FechaVencimiento.DayNumber - hoy.DayNumber;
            var nombreCompleto = BuildNombreCompleto(
                item.Nombres,
                item.ApellidoPaterno,
                item.ApellidoMaterno);

            sb.Append($$"""
              <tr>
                <td style="padding:8px;border-bottom:1px solid #f1f5f9">{{WebUtility.HtmlEncode(nombreCompleto)}}</td>
                <td style="padding:8px;border-bottom:1px solid #f1f5f9">{{WebUtility.HtmlEncode(item.Tipo)}}</td>
                <td style="padding:8px;border-bottom:1px solid #f1f5f9">{{item.FechaVencimiento:dd/MM/yyyy}}</td>
                <td style="padding:8px;border-bottom:1px solid #f1f5f9">{{diasRestantes}}</td>
                <td style="padding:8px;border-bottom:1px solid #f1f5f9">{{WebUtility.HtmlEncode(item.SucursalNombre ?? "Sin sucursal")}}</td>
              </tr>
            """);
        }

        sb.Append("""
            </tbody>
          </table>
        </div>
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

    private sealed class DocumentoPorVencerItem
    {
        public int Id { get; set; }
        public int EmpleadoId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public DateOnly FechaVencimiento { get; set; }
        public string? NumEmpleado { get; set; }
        public string? Nombres { get; set; }
        public string? ApellidoPaterno { get; set; }
        public string? ApellidoMaterno { get; set; }
        public string? SucursalNombre { get; set; }
    }
}