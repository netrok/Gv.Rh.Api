using Gv.Rh.Application.DTOs.Reclutamiento;
using Gv.Rh.Application.DTOs.Reclutamiento.Reportes;
using Gv.Rh.Application.Interfaces.Reclutamiento;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public sealed class ReclutamientoController : ControllerBase
{
    private readonly IReclutamientoReporteService _reporteService;
    private readonly RhDbContext _context;

    public ReclutamientoController(
        IReclutamientoReporteService reporteService,
        RhDbContext context)
    {
        _reporteService = reporteService;
        _context = context;
    }

    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] ReclutamientoReporteFiltroDto filtro,
        CancellationToken cancellationToken)
    {
        var content = await _reporteService.ExportarXlsxAsync(filtro, cancellationToken);
        var fileName = $"reclutamiento_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] ReclutamientoReporteFiltroDto filtro,
        CancellationToken cancellationToken)
    {
        var content = await _reporteService.ExportarPdfAsync(filtro, cancellationToken);
        var fileName = $"reclutamiento_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

        return File(content, "application/pdf", fileName);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ReclutamientoDashboardDto>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var monthStartUtc = new DateTime(
            utcNow.Year,
            utcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var stalledSinceUtc = utcNow.AddDays(-7);

        var vacantesRaw = await _context.Vacantes
            .AsNoTracking()
            .Select(v => new
            {
                v.Id,
                v.Activo,
                Estatus = v.Estatus.ToString(),
                v.Titulo,
                Departamento = v.Departamento != null ? v.Departamento.Nombre : null,
                Sucursal = v.Sucursal != null ? v.Sucursal.Nombre : null
            })
            .ToListAsync(cancellationToken);

        var candidatosRaw = await _context.Candidatos
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Activo,
                c.Nombres,
                c.ApellidoPaterno,
                c.ApellidoMaterno,
                c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var postulacionesRaw = await _context.Postulaciones
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.CandidatoId,
                p.VacanteId,
                Etapa = p.EtapaActual.ToString(),
                p.FechaPostulacionUtc,
                p.FechaUltimoMovimientoUtc,
                VacanteTitulo = p.Vacante != null ? p.Vacante.Titulo : null,
                VacanteDepartamento = p.Vacante != null && p.Vacante.Departamento != null
                    ? p.Vacante.Departamento.Nombre
                    : null,
                VacanteSucursal = p.Vacante != null && p.Vacante.Sucursal != null
                    ? p.Vacante.Sucursal.Nombre
                    : null,
                CandidatoNombre = p.Candidato != null
                    ? (
                        (p.Candidato.Nombres ?? "") + " " +
                        (p.Candidato.ApellidoPaterno ?? "") + " " +
                        (p.Candidato.ApellidoMaterno ?? "")
                    ).Trim()
                    : "Sin nombre"
            })
            .ToListAsync(cancellationToken);

        var vacantes = vacantesRaw
            .Select(v => new
            {
                v.Id,
                v.Activo,
                Estatus = NormalizeText(v.Estatus),
                Titulo = string.IsNullOrWhiteSpace(v.Titulo)
                    ? $"Vacante {v.Id}"
                    : v.Titulo.Trim(),
                Departamento = string.IsNullOrWhiteSpace(v.Departamento) ? null : v.Departamento.Trim(),
                Sucursal = string.IsNullOrWhiteSpace(v.Sucursal) ? null : v.Sucursal.Trim()
            })
            .ToList();

        var candidatos = candidatosRaw
            .Select(c => new
            {
                c.Id,
                c.Activo,
                NombreCompleto = BuildFullName(c.Nombres, c.ApellidoPaterno, c.ApellidoMaterno),
                FechaRegistroUtc = c.CreatedAtUtc
            })
            .ToList();

        var postulaciones = postulacionesRaw
            .Select(p => new
            {
                p.Id,
                p.CandidatoId,
                p.VacanteId,
                Etapa = NormalizeText(p.Etapa),
                FechaPostulacionUtc = p.FechaPostulacionUtc,
                FechaMovimientoUtc = p.FechaUltimoMovimientoUtc,
                VacanteTitulo = string.IsNullOrWhiteSpace(p.VacanteTitulo)
                    ? "Sin vacante"
                    : p.VacanteTitulo.Trim(),
                VacanteDepartamento = string.IsNullOrWhiteSpace(p.VacanteDepartamento)
                    ? null
                    : p.VacanteDepartamento.Trim(),
                VacanteSucursal = string.IsNullOrWhiteSpace(p.VacanteSucursal)
                    ? null
                    : p.VacanteSucursal.Trim(),
                CandidatoNombre = string.IsNullOrWhiteSpace(p.CandidatoNombre)
                    ? "Sin nombre"
                    : p.CandidatoNombre.Trim()
            })
            .ToList();

        var vacantesActivas = vacantes.Count(v => v.Activo && !IsClosedVacancyStatus(v.Estatus));
        var vacantesCerradas = vacantes.Count(v => IsClosedVacancyStatus(v.Estatus));
        var candidatosTotales = candidatos.Count(c => c.Activo);
        var candidatosEnProceso = postulaciones.Count(p => !IsClosedStage(p.Etapa));

        var contratadosMes = postulaciones.Count(p =>
            p.Etapa == "CONTRATADO" &&
            p.FechaMovimientoUtc >= monthStartUtc);

        var pipelinePorEtapa = postulaciones
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Etapa) ? "SIN_ETAPA" : p.Etapa)
            .Select(g => new DashboardCountItemDto
            {
                Nombre = g.Key,
                Total = g.Count()
            })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Nombre)
            .ToList();

        var vacantesTop = postulaciones
            .GroupBy(p => new
            {
                p.VacanteId,
                p.VacanteTitulo,
                p.VacanteDepartamento,
                p.VacanteSucursal
            })
            .Select(g =>
            {
                var vacante = vacantes.FirstOrDefault(v => v.Id == g.Key.VacanteId);

                return new DashboardVacanteResumenDto
                {
                    Id = g.Key.VacanteId,
                    Titulo = g.Key.VacanteTitulo,
                    Departamento = g.Key.VacanteDepartamento,
                    Sucursal = g.Key.VacanteSucursal,
                    Estatus = vacante?.Estatus ?? "SIN_ESTATUS",
                    TotalCandidatos = g.Select(x => x.CandidatoId).Distinct().Count()
                };
            })
            .OrderByDescending(x => x.TotalCandidatos)
            .ThenBy(x => x.Titulo)
            .Take(5)
            .ToList();

        var candidatosRecientes = postulaciones
            .OrderByDescending(p => p.FechaPostulacionUtc)
            .ThenByDescending(p => p.Id)
            .Take(8)
            .Select(p => new DashboardCandidatoResumenDto
            {
                Id = p.CandidatoId,
                NombreCompleto = p.CandidatoNombre,
                Vacante = p.VacanteTitulo,
                Etapa = p.Etapa,
                FechaRegistroUtc = p.FechaPostulacionUtc
            })
            .ToList();

        var vacantesSinCandidatos = vacantes.Count(v =>
            v.Activo &&
            !IsClosedVacancyStatus(v.Estatus) &&
            postulaciones.All(p => p.VacanteId != v.Id));

        var ofertasPendientes = postulaciones.Count(p => p.Etapa == "OFERTA");

        var procesosEstancados = postulaciones.Count(p =>
            !IsClosedStage(p.Etapa) &&
            p.FechaMovimientoUtc <= stalledSinceUtc);

        var alertas = new List<DashboardAlertaDto>();

        if (vacantesSinCandidatos > 0)
        {
            alertas.Add(new DashboardAlertaDto
            {
                Tipo = "warning",
                Titulo = "Vacantes sin candidatos",
                Descripcion = "Hay vacantes activas sin postulaciones registradas.",
                Total = vacantesSinCandidatos
            });
        }

        if (ofertasPendientes > 0)
        {
            alertas.Add(new DashboardAlertaDto
            {
                Tipo = "info",
                Titulo = "Ofertas pendientes",
                Descripcion = "Existen procesos en etapa de oferta que requieren seguimiento.",
                Total = ofertasPendientes
            });
        }

        if (procesosEstancados > 0)
        {
            alertas.Add(new DashboardAlertaDto
            {
                Tipo = "warning",
                Titulo = "Procesos estancados",
                Descripcion = "Hay postulaciones sin movimiento en los últimos 7 días.",
                Total = procesosEstancados
            });
        }

        if (alertas.Count == 0)
        {
            alertas.Add(new DashboardAlertaDto
            {
                Tipo = "success",
                Titulo = "Sin alertas relevantes",
                Descripcion = "El módulo de reclutamiento no presenta pendientes críticos."
            });
        }

        var dto = new ReclutamientoDashboardDto
        {
            VacantesActivas = vacantesActivas,
            VacantesCerradas = vacantesCerradas,
            CandidatosTotales = candidatosTotales,
            CandidatosEnProceso = candidatosEnProceso,
            ContratadosMes = contratadosMes,
            PipelinePorEtapa = pipelinePorEtapa,
            VacantesTop = vacantesTop,
            CandidatosRecientes = candidatosRecientes,
            Alertas = alertas
        };

        return Ok(dto);
    }

    private static string NormalizeText(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static bool IsClosedStage(string stage)
    {
        return stage is "CONTRATADO" or "RECHAZADO" or "DESCARTADO";
    }

    private static bool IsClosedVacancyStatus(string status)
    {
        return status is "CERRADA" or "CERRADO" or "FINALIZADA" or "CUBIERTA";
    }

    private static string BuildFullName(
        string? nombres,
        string? apellidoPaterno,
        string? apellidoMaterno)
    {
        return string.Join(
                " ",
                new[] { nombres, apellidoPaterno, apellidoMaterno }
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim()))
            .Trim();
    }
}