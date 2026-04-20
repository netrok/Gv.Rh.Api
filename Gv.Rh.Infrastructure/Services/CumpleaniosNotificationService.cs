using Gv.Rh.Application.DTOs.Cumpleanios;
using Gv.Rh.Application.Interfaces;
using System.Net;
using System.Text;

namespace Gv.Rh.Infrastructure.Services;

public sealed class CumpleaniosNotificationService : ICumpleaniosNotificationService
{
    private readonly ICumpleaniosService _cumpleaniosService;
    private readonly IEmailService _emailService;

    public CumpleaniosNotificationService(
        ICumpleaniosService cumpleaniosService,
        IEmailService emailService)
    {
        _cumpleaniosService = cumpleaniosService;
        _emailService = emailService;
    }

    public async Task<int> EnviarCumpleaniosHoyAsync(
        bool incluirEdad,
        CancellationToken cancellationToken = default)
    {
        var cumpleanios = await _cumpleaniosService.GetHoyAsync(
            sucursalId: null,
            departamentoId: null,
            cancellationToken);

        if (cumpleanios.Count == 0)
        {
            return 0;
        }

        var html = BuildHtml(cumpleanios, incluirEdad);

        await _emailService.SendAsync(
            to: "ernesto.ramirez@gv.com.mx",
            subject: $"Cumpleaños de hoy - {DateTime.Today:dd/MM/yyyy}",
            htmlBody: html,
            cancellationToken: cancellationToken);

        return cumpleanios.Count;
    }

    private static string BuildHtml(
        IReadOnlyList<CumpleaniosItemDto> items,
        bool incluirEdad)
    {
        var sb = new StringBuilder();

        sb.Append("""
        <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937">
          <h2 style="margin:0 0 12px 0;color:#0f172a">Cumpleaños del día</h2>
          <p style="margin:0 0 16px 0">Personal que celebra hoy:</p>
          <table style="border-collapse:collapse;width:100%">
            <thead>
              <tr>
                <th style="text-align:left;padding:8px;border-bottom:1px solid #e5e7eb">Empleado</th>
                <th style="text-align:left;padding:8px;border-bottom:1px solid #e5e7eb">Puesto</th>
                <th style="text-align:left;padding:8px;border-bottom:1px solid #e5e7eb">Sucursal</th>
                <th style="text-align:left;padding:8px;border-bottom:1px solid #e5e7eb">Detalle</th>
              </tr>
            </thead>
            <tbody>
        """);

        foreach (var item in items)
        {
            var detalle = incluirEdad
                ? $"Cumple {item.EdadQueCumple} años"
                : "Cumpleaños del día";

            sb.Append($$"""
              <tr>
                <td style="padding:8px;border-bottom:1px solid #f1f5f9">{{WebUtility.HtmlEncode(item.NombreCompleto)}}</td>
                <td style="padding:8px;border-bottom:1px solid #f1f5f9">{{WebUtility.HtmlEncode(item.PuestoNombre ?? "Sin puesto")}}</td>
                <td style="padding:8px;border-bottom:1px solid #f1f5f9">{{WebUtility.HtmlEncode(item.SucursalNombre ?? "Sin sucursal")}}</td>
                <td style="padding:8px;border-bottom:1px solid #f1f5f9">{{WebUtility.HtmlEncode(detalle)}}</td>
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
}