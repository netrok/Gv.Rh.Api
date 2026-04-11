using Gv.Rh.Application.DTOs.Dashboard;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN,RRHH")]
[Route("api/Dashboard")]
public class DashboardDocumentosController : ControllerBase
{
    private readonly RhDbContext _db;

    private static readonly TipoDocumentoEmpleado[] RequiredTipos =
    [
        TipoDocumentoEmpleado.Ine,
        TipoDocumentoEmpleado.Curp,
        TipoDocumentoEmpleado.Rfc,
        TipoDocumentoEmpleado.Nss,
        TipoDocumentoEmpleado.ActaNacimiento,
        TipoDocumentoEmpleado.ComprobanteDomicilio,
        TipoDocumentoEmpleado.Contrato,
        TipoDocumentoEmpleado.CartaPolicia
    ];

    public DashboardDocumentosController(RhDbContext db)
    {
        _db = db;
    }

    [HttpGet("documentos")]
    public async Task<ActionResult<DashboardDocumentosResumenDto>> GetDocumentosResumen(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var warningDate = today.AddDays(30);

        var empleados = await _db.Empleados
            .AsNoTracking()
            .Where(x => x.Activo)
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Include(x => x.Sucursal)
            .ToListAsync(cancellationToken);

        var empleadoIds = empleados.Select(x => x.Id).ToList();

        var documentos = empleadoIds.Count == 0
            ? new List<EmpleadoDocumento>()
            : await _db.EmpleadoDocumentos
                .AsNoTracking()
                .Where(x => empleadoIds.Contains(x.EmpleadoId) && x.Activo)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ThenByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);

        var latestByEmpleadoAndTipo = documentos
            .GroupBy(x => new { x.EmpleadoId, x.Tipo })
            .ToDictionary(
                g => (g.Key.EmpleadoId, g.Key.Tipo),
                g => g.First());

        var alertas = new List<DashboardDocumentoAlertaItemDto>();
        var documentosPorVencer = 0;
        var documentosVencidos = 0;
        var expedientesIncompletos = 0;

        foreach (var empleado in empleados)
        {
            var totalFaltantes = 0;
            var totalPorVencer = 0;
            var totalVencidos = 0;
            var totalCargados = 0;

            foreach (var tipo in RequiredTipos)
            {
                latestByEmpleadoAndTipo.TryGetValue((empleado.Id, tipo), out var documento);

                if (documento is null)
                {
                    totalFaltantes++;
                    continue;
                }

                if (!documento.FechaVencimiento.HasValue)
                {
                    totalCargados++;
                    continue;
                }

                var fechaVencimiento = documento.FechaVencimiento.Value;

                if (fechaVencimiento < today)
                {
                    totalVencidos++;
                    documentosVencidos++;
                    continue;
                }

                if (fechaVencimiento <= warningDate)
                {
                    totalPorVencer++;
                    documentosPorVencer++;
                    totalCargados++;
                    continue;
                }

                totalCargados++;
            }

            var porcentajeCumplimiento = RequiredTipos.Length == 0
                ? 100m
                : Math.Round((decimal)totalCargados / RequiredTipos.Length * 100m, 2);

            var expedienteIncompleto = totalFaltantes > 0 || totalVencidos > 0;
            if (expedienteIncompleto)
            {
                expedientesIncompletos++;
            }

            if (totalFaltantes > 0 || totalPorVencer > 0 || totalVencidos > 0)
            {
                alertas.Add(new DashboardDocumentoAlertaItemDto
                {
                    EmpleadoId = empleado.Id,
                    NumEmpleado = empleado.NumEmpleado,
                    NombreEmpleado = BuildNombreCompleto(empleado),
                    DepartamentoNombre = empleado.Departamento?.Nombre,
                    PuestoNombre = empleado.Puesto?.Nombre,
                    SucursalNombre = empleado.Sucursal?.Nombre,
                    TotalFaltantes = totalFaltantes,
                    TotalPorVencer = totalPorVencer,
                    TotalVencidos = totalVencidos,
                    PorcentajeCumplimiento = porcentajeCumplimiento
                });
            }
        }

        var dto = new DashboardDocumentosResumenDto
        {
            TotalEmpleadosActivos = empleados.Count,
            ExpedientesIncompletos = expedientesIncompletos,
            DocumentosPorVencer = documentosPorVencer,
            DocumentosVencidos = documentosVencidos,
            Alertas = alertas
                .OrderByDescending(x => x.TotalVencidos)
                .ThenByDescending(x => x.TotalFaltantes)
                .ThenByDescending(x => x.TotalPorVencer)
                .ThenBy(x => x.PorcentajeCumplimiento)
                .ThenBy(x => x.NombreEmpleado)
                .Take(15)
                .ToList()
        };

        return Ok(dto);
    }

    private static string BuildNombreCompleto(Empleado empleado)
    {
        return string.Join(" ", new[]
        {
            empleado.Nombres?.Trim(),
            empleado.ApellidoPaterno?.Trim(),
            empleado.ApellidoMaterno?.Trim()
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}