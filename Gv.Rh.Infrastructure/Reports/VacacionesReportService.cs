using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Vacaciones.Reports;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Gv.Rh.Infrastructure.Reports.Common;
using Gv.Rh.Infrastructure.Reports.Excel;
using Gv.Rh.Infrastructure.Reports.Pdf;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace Gv.Rh.Infrastructure.Reports;

public sealed class VacacionesReportService : IVacacionesReportService
{
    private readonly RhDbContext _context;

    public VacacionesReportService(RhDbContext context)
    {
        _context = context;
    }

    public async Task<VacacionesSaldosReporteResultDto> GetSaldosAsync(
        VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetSaldosRowsAsync(query, cancellationToken);
        var fechaCorte = ResolveFechaCorte(query);

        return new VacacionesSaldosReporteResultDto
        {
            FechaCorte = fechaCorte,
            TotalRegistros = rows.Count,
            EmpleadosConSaldo = rows
                .Where(x => x.Saldo > 0)
                .Select(x => x.EmpleadoId)
                .Distinct()
                .Count(),
            PeriodosVencidos = rows.Count(x => x.EstaVencido),
            TotalDiasDerecho = rows.Sum(x => x.DiasDerecho),
            TotalDiasTomados = rows.Sum(x => x.DiasTomados),
            TotalDiasPagados = rows.Sum(x => x.DiasPagados),
            TotalDiasVencidos = rows.Sum(x => x.DiasVencidos),
            TotalSaldo = rows.Sum(x => x.Saldo),
            Items = rows
        };
    }

    public async Task<ReportFileDto> BuildSaldosXlsxAsync(
        VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await GetSaldosAsync(query, cancellationToken);
        var filterLabels = await ResolveFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        using var workbook = new XLWorkbook();
        CorporateExcelStyles.ApplyWorkbookDefaults(workbook);

        BuildResumenSheet(workbook, result, filterLabels, generatedAtUtc);
        BuildDetalleSheet(workbook, result.Items, filterLabels, generatedAtUtc);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ReportFileDto
        {
            Content = stream.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = BuildFileName(query, "xlsx")
        };
    }

    public async Task<ReportFileDto> BuildSaldosPdfAsync(
        VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await GetSaldosAsync(query, cancellationToken);
        var filterLabels = await ResolveFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        var pdfBytes = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(CorporateReportPalette.Ink900));

                page.Header().Element(container =>
                    CorporatePdfBlocks.ComposeReportHeader(
                        container,
                        "Reporte de saldos de vacaciones",
                        "Resumen ejecutivo y detalle operativo",
                        generatedAtUtc,
                        rightBadgeText: "Vacaciones"));

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(container =>
                        CorporatePdfBlocks.ComposeFiltersPanel(
                            container,
                            "Filtros aplicados",
                            BuildFilterItems(filterLabels)));

                    column.Item().Element(container =>
                        CorporatePdfBlocks.ComposeKpiRow(
                            container,
                            (
                                "Total registros",
                                result.TotalRegistros.ToString(CultureInfo.InvariantCulture),
                                "Periodos visibles",
                                CorporateReportPalette.KpiPrimary
                            ),
                            (
                                "Empleados con saldo",
                                result.EmpleadosConSaldo.ToString(CultureInfo.InvariantCulture),
                                "Saldo mayor a cero",
                                CorporateReportPalette.KpiSuccess
                            ),
                            (
                                "Saldo total",
                                FormatDecimal(result.TotalSaldo),
                                "Días disponibles",
                                CorporateReportPalette.KpiTeal
                            ),
                            (
                                "Vencidos",
                                result.PeriodosVencidos.ToString(CultureInfo.InvariantCulture),
                                "Periodos con saldo vencido",
                                CorporateReportPalette.Danger
                            ),
                            (
                                "Días vencidos",
                                FormatDecimal(result.TotalDiasVencidos),
                                "Acumulado vencido",
                                CorporateReportPalette.KpiWarning
                            )));

                    column.Item().Element(container => ComposeDistributionSection(container, result.Items));
                    column.Item().Element(container => ComposeDetailSection(container, result.Items));
                });

                page.Footer().Element(container =>
                    CorporatePdfBlocks.ComposeReportFooter(container, generatedAtUtc));
            });
        }).GeneratePdf();

        return new ReportFileDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = BuildFileName(query, "pdf")
        };
    }

    public async Task<VacacionesKardexReporteResultDto> GetKardexAsync(
        VacacionesKardexReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetKardexRowsAsync(query, cancellationToken);

        return new VacacionesKardexReporteResultDto
        {
            TotalMovimientos = rows.Count,
            TotalDiasPositivos = rows.Where(x => x.Dias > 0).Sum(x => x.Dias),
            TotalDiasNegativos = rows.Where(x => x.Dias < 0).Sum(x => x.Dias),
            BalanceDias = rows.Sum(x => x.Dias),
            MovimientosDisfrute = rows.Count(x =>
                string.Equals(x.TipoMovimiento, TipoMovimientoVacacion.DISFRUTE.ToString(), StringComparison.OrdinalIgnoreCase)),
            MovimientosAjuste = rows.Count(x =>
                string.Equals(x.TipoMovimiento, TipoMovimientoVacacion.AJUSTE_POSITIVO.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.TipoMovimiento, TipoMovimientoVacacion.AJUSTE_NEGATIVO.ToString(), StringComparison.OrdinalIgnoreCase)),
            MovimientosImportacion = rows.Count(x =>
                !string.IsNullOrWhiteSpace(x.ImportacionArchivo) ||
                string.Equals(x.Origen, "EXCEL_LEGACY", StringComparison.OrdinalIgnoreCase)),
            Items = rows
        };
    }

    public async Task<ReportFileDto> BuildKardexXlsxAsync(
        VacacionesKardexReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await GetKardexAsync(query, cancellationToken);
        var filterLabels = await ResolveKardexFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        using var workbook = new XLWorkbook();
        CorporateExcelStyles.ApplyWorkbookDefaults(workbook);

        BuildKardexResumenSheet(workbook, result, filterLabels, generatedAtUtc);
        BuildKardexDetalleSheet(workbook, result.Items, filterLabels, generatedAtUtc);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ReportFileDto
        {
            Content = stream.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = BuildKardexFileName(query, "xlsx")
        };
    }

    public async Task<ReportFileDto> BuildKardexPdfAsync(
        VacacionesKardexReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await GetKardexAsync(query, cancellationToken);
        var filterLabels = await ResolveKardexFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        var pdfBytes = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8.3f).FontColor(CorporateReportPalette.Ink900));

                page.Header().Element(container =>
                    CorporatePdfBlocks.ComposeReportHeader(
                        container,
                        "Reporte de kárdex de vacaciones",
                        "Auditoría de movimientos, saldos y origen de cambios",
                        generatedAtUtc,
                        rightBadgeText: "Vacaciones"));

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(container =>
                        CorporatePdfBlocks.ComposeFiltersPanel(
                            container,
                            "Filtros aplicados",
                            BuildKardexFilterItems(filterLabels)));

                    column.Item().Element(container =>
                        CorporatePdfBlocks.ComposeKpiRow(
                            container,
                            (
                                "Movimientos",
                                result.TotalMovimientos.ToString(CultureInfo.InvariantCulture),
                                "Registros visibles",
                                CorporateReportPalette.KpiPrimary
                            ),
                            (
                                "Días positivos",
                                FormatDecimal(result.TotalDiasPositivos),
                                "Altas / ajustes / aperturas",
                                CorporateReportPalette.KpiSuccess
                            ),
                            (
                                "Días negativos",
                                FormatDecimal(result.TotalDiasNegativos),
                                "Disfrutes / pagos / vencimientos",
                                CorporateReportPalette.Danger
                            ),
                            (
                                "Balance",
                                FormatDecimal(result.BalanceDias),
                                "Suma neta de días",
                                CorporateReportPalette.KpiTeal
                            ),
                            (
                                "Importación",
                                result.MovimientosImportacion.ToString(CultureInfo.InvariantCulture),
                                "Movimientos legacy",
                                CorporateReportPalette.KpiWarning
                            )));

                    column.Item().Element(container => ComposeKardexDistributionSection(container, result.Items));
                    column.Item().Element(container => ComposeKardexDetailSection(container, result.Items));
                });

                page.Footer().Element(container =>
                    CorporatePdfBlocks.ComposeReportFooter(container, generatedAtUtc));
            });
        }).GeneratePdf();

        return new ReportFileDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = BuildKardexFileName(query, "pdf")
        };
    }

    private IQueryable<VacacionPeriodo> BuildBaseQuery(VacacionesSaldosReporteQueryDto query)
    {
        var periodosQuery = _context.VacacionPeriodos
            .AsNoTracking()
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Sucursal)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Departamento)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Puesto)
            .AsQueryable();

        if (query.EmpleadoId.HasValue)
            periodosQuery = periodosQuery.Where(x => x.EmpleadoId == query.EmpleadoId.Value);

        if (query.SucursalId.HasValue)
            periodosQuery = periodosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.SucursalId == query.SucursalId.Value);

        if (query.DepartamentoId.HasValue)
            periodosQuery = periodosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.DepartamentoId == query.DepartamentoId.Value);

        if (query.PuestoId.HasValue)
            periodosQuery = periodosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.PuestoId == query.PuestoId.Value);

        if (query.SoloActivos == true)
            periodosQuery = periodosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.Activo);

        if (!string.IsNullOrWhiteSpace(query.EstatusLaboral) &&
            Enum.TryParse<EstatusLaboralEmpleado>(
                query.EstatusLaboral.Trim(),
                ignoreCase: true,
                out var estatusLaboral))
        {
            periodosQuery = periodosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.EstatusLaboralActual == estatusLaboral);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();

            periodosQuery = periodosQuery.Where(x =>
                x.Empleado != null &&
                (
                    EF.Functions.ILike(x.Empleado.NumEmpleado, $"%{term}%") ||
                    EF.Functions.ILike(x.Empleado.Nombres, $"%{term}%") ||
                    EF.Functions.ILike(x.Empleado.ApellidoPaterno, $"%{term}%") ||
                    (x.Empleado.ApellidoMaterno != null && EF.Functions.ILike(x.Empleado.ApellidoMaterno, $"%{term}%")) ||
                    (x.Empleado.Rfc != null && EF.Functions.ILike(x.Empleado.Rfc, $"%{term}%")) ||
                    (x.Empleado.Nss != null && EF.Functions.ILike(x.Empleado.Nss, $"%{term}%")) ||
                    (x.Empleado.Sucursal != null && EF.Functions.ILike(x.Empleado.Sucursal.Nombre, $"%{term}%")) ||
                    (x.Empleado.Departamento != null && EF.Functions.ILike(x.Empleado.Departamento.Nombre, $"%{term}%")) ||
                    (x.Empleado.Puesto != null && EF.Functions.ILike(x.Empleado.Puesto.Nombre, $"%{term}%"))
                ));
        }

        return periodosQuery;
    }

    private IQueryable<VacacionMovimiento> BuildKardexBaseQuery(VacacionesKardexReporteQueryDto query)
    {
        var movimientosQuery = _context.VacacionMovimientos
            .AsNoTracking()
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Sucursal)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Departamento)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Puesto)
            .Include(x => x.VacacionPeriodo)
            .Include(x => x.UsuarioResponsable)
            .AsQueryable();

        if (query.EmpleadoId.HasValue)
            movimientosQuery = movimientosQuery.Where(x => x.EmpleadoId == query.EmpleadoId.Value);

        if (query.SucursalId.HasValue)
            movimientosQuery = movimientosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.SucursalId == query.SucursalId.Value);

        if (query.DepartamentoId.HasValue)
            movimientosQuery = movimientosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.DepartamentoId == query.DepartamentoId.Value);

        if (query.PuestoId.HasValue)
            movimientosQuery = movimientosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.PuestoId == query.PuestoId.Value);

        if (query.SoloActivos == true)
            movimientosQuery = movimientosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.Activo);

        if (!string.IsNullOrWhiteSpace(query.EstatusLaboral) &&
            Enum.TryParse<EstatusLaboralEmpleado>(
                query.EstatusLaboral.Trim(),
                ignoreCase: true,
                out var estatusLaboral))
        {
            movimientosQuery = movimientosQuery.Where(x =>
                x.Empleado != null &&
                x.Empleado.EstatusLaboralActual == estatusLaboral);
        }

        if (!string.IsNullOrWhiteSpace(query.TipoMovimiento) &&
            Enum.TryParse<TipoMovimientoVacacion>(
                query.TipoMovimiento.Trim(),
                ignoreCase: true,
                out var tipoMovimiento))
        {
            movimientosQuery = movimientosQuery.Where(x => x.TipoMovimiento == tipoMovimiento);
        }

        if (!string.IsNullOrWhiteSpace(query.Origen))
        {
            var origen = query.Origen.Trim();

            movimientosQuery = movimientosQuery.Where(x =>
                x.Origen != null &&
                EF.Functions.ILike(x.Origen, $"%{origen}%"));
        }

        if (query.FechaDesde.HasValue)
            movimientosQuery = movimientosQuery.Where(x => x.FechaMovimiento >= query.FechaDesde.Value);

        if (query.FechaHasta.HasValue)
            movimientosQuery = movimientosQuery.Where(x => x.FechaMovimiento <= query.FechaHasta.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();

            movimientosQuery = movimientosQuery.Where(x =>
                x.Empleado != null &&
                (
                    EF.Functions.ILike(x.Empleado.NumEmpleado, $"%{term}%") ||
                    EF.Functions.ILike(x.Empleado.Nombres, $"%{term}%") ||
                    EF.Functions.ILike(x.Empleado.ApellidoPaterno, $"%{term}%") ||
                    (x.Empleado.ApellidoMaterno != null && EF.Functions.ILike(x.Empleado.ApellidoMaterno, $"%{term}%")) ||
                    (x.Empleado.Rfc != null && EF.Functions.ILike(x.Empleado.Rfc, $"%{term}%")) ||
                    (x.Empleado.Nss != null && EF.Functions.ILike(x.Empleado.Nss, $"%{term}%")) ||
                    (x.Empleado.Sucursal != null && EF.Functions.ILike(x.Empleado.Sucursal.Nombre, $"%{term}%")) ||
                    (x.Empleado.Departamento != null && EF.Functions.ILike(x.Empleado.Departamento.Nombre, $"%{term}%")) ||
                    (x.Empleado.Puesto != null && EF.Functions.ILike(x.Empleado.Puesto.Nombre, $"%{term}%")) ||
                    (x.Referencia != null && EF.Functions.ILike(x.Referencia, $"%{term}%")) ||
                    (x.Comentario != null && EF.Functions.ILike(x.Comentario, $"%{term}%")) ||
                    (x.Origen != null && EF.Functions.ILike(x.Origen, $"%{term}%")) ||
                    (x.ImportacionArchivo != null && EF.Functions.ILike(x.ImportacionArchivo, $"%{term}%")) ||
                    (x.ImportacionHoja != null && EF.Functions.ILike(x.ImportacionHoja, $"%{term}%")) ||
                    (x.UsuarioResponsable != null && EF.Functions.ILike(x.UsuarioResponsable.FullName, $"%{term}%")) ||
                    (x.UsuarioResponsable != null && EF.Functions.ILike(x.UsuarioResponsable.Email, $"%{term}%"))
                ));
        }

        return movimientosQuery;
    }

    private async Task<List<VacacionesSaldosReporteRowDto>> GetSaldosRowsAsync(
        VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var fechaCorte = ResolveFechaCorte(query);

        var rawRows = await BuildBaseQuery(query)
            .OrderBy(x => x.Empleado!.NumEmpleado)
            .ThenBy(x => x.AnioServicio)
            .ThenBy(x => x.FechaInicio)
            .Select(x => new
            {
                VacacionPeriodoId = x.Id,
                x.EmpleadoId,
                NumEmpleado = x.Empleado != null ? x.Empleado.NumEmpleado : string.Empty,
                Nombres = x.Empleado != null ? x.Empleado.Nombres : string.Empty,
                ApellidoPaterno = x.Empleado != null ? x.Empleado.ApellidoPaterno : string.Empty,
                ApellidoMaterno = x.Empleado != null ? x.Empleado.ApellidoMaterno : null,
                Sucursal = x.Empleado != null && x.Empleado.Sucursal != null ? x.Empleado.Sucursal.Nombre : null,
                Departamento = x.Empleado != null && x.Empleado.Departamento != null ? x.Empleado.Departamento.Nombre : null,
                Puesto = x.Empleado != null && x.Empleado.Puesto != null ? x.Empleado.Puesto.Nombre : null,
                EstatusLaboral = x.Empleado != null ? x.Empleado.EstatusLaboralActual.ToString() : string.Empty,
                Activo = x.Empleado != null && x.Empleado.Activo,
                FechaIngreso = x.Empleado != null ? x.Empleado.FechaIngreso : default,
                x.AnioServicio,
                x.FechaInicio,
                x.FechaFin,
                x.FechaLimiteDisfrute,
                x.DiasDerecho,
                x.DiasTomados,
                x.DiasPagados,
                x.DiasAjustados,
                x.DiasVencidos,
                x.Saldo,
                EstatusPeriodo = x.Estatus.ToString(),
                x.PrimaPagada
            })
            .ToListAsync(cancellationToken);

        var rows = rawRows.Select(x =>
        {
            var estaVencido = x.Saldo > 0 && x.FechaLimiteDisfrute < fechaCorte;
            var diasParaVencer = x.FechaLimiteDisfrute.DayNumber - fechaCorte.DayNumber;

            return new VacacionesSaldosReporteRowDto
            {
                EmpleadoId = x.EmpleadoId,
                NumEmpleado = x.NumEmpleado,
                NombreEmpleado = CorporateReportFormatters.CombineFullName(
                    x.Nombres,
                    x.ApellidoPaterno,
                    x.ApellidoMaterno),
                Sucursal = x.Sucursal,
                Departamento = x.Departamento,
                Puesto = x.Puesto,
                EstatusLaboral = x.EstatusLaboral,
                Activo = x.Activo,
                FechaIngreso = x.FechaIngreso,
                VacacionPeriodoId = x.VacacionPeriodoId,
                AnioServicio = x.AnioServicio,
                FechaInicio = x.FechaInicio,
                FechaFin = x.FechaFin,
                FechaLimiteDisfrute = x.FechaLimiteDisfrute,
                DiasDerecho = x.DiasDerecho,
                DiasTomados = x.DiasTomados,
                DiasPagados = x.DiasPagados,
                DiasAjustados = x.DiasAjustados,
                DiasVencidos = x.DiasVencidos,
                Saldo = x.Saldo,
                EstatusPeriodo = x.EstatusPeriodo,
                PrimaPagada = x.PrimaPagada,
                EstaVencido = estaVencido,
                DiasParaVencer = diasParaVencer
            };
        }).ToList();

        if (query.SoloConSaldo == true)
            rows = rows.Where(x => x.Saldo > 0).ToList();

        if (query.SoloVencidos == true)
            rows = rows.Where(x => x.EstaVencido).ToList();

        return rows;
    }

    private async Task<List<VacacionesKardexReporteRowDto>> GetKardexRowsAsync(
        VacacionesKardexReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rawRows = await BuildKardexBaseQuery(query)
            .OrderByDescending(x => x.FechaMovimiento)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                MovimientoId = x.Id,
                x.EmpleadoId,
                NumEmpleado = x.Empleado != null ? x.Empleado.NumEmpleado : string.Empty,
                Nombres = x.Empleado != null ? x.Empleado.Nombres : string.Empty,
                ApellidoPaterno = x.Empleado != null ? x.Empleado.ApellidoPaterno : string.Empty,
                ApellidoMaterno = x.Empleado != null ? x.Empleado.ApellidoMaterno : null,
                Sucursal = x.Empleado != null && x.Empleado.Sucursal != null ? x.Empleado.Sucursal.Nombre : null,
                Departamento = x.Empleado != null && x.Empleado.Departamento != null ? x.Empleado.Departamento.Nombre : null,
                Puesto = x.Empleado != null && x.Empleado.Puesto != null ? x.Empleado.Puesto.Nombre : null,
                EstatusLaboral = x.Empleado != null ? x.Empleado.EstatusLaboralActual.ToString() : string.Empty,
                Activo = x.Empleado != null && x.Empleado.Activo,
                x.VacacionPeriodoId,
                AnioServicio = x.VacacionPeriodo != null ? x.VacacionPeriodo.AnioServicio : 0,
                PeriodoFechaInicio = x.VacacionPeriodo != null ? x.VacacionPeriodo.FechaInicio : default,
                PeriodoFechaFin = x.VacacionPeriodo != null ? x.VacacionPeriodo.FechaFin : default,
                TipoMovimiento = x.TipoMovimiento.ToString(),
                x.FechaMovimiento,
                x.FechaInicioDisfrute,
                x.FechaFinDisfrute,
                x.Dias,
                x.SaldoAntes,
                x.SaldoDespues,
                x.Referencia,
                x.Comentario,
                x.UsuarioResponsableId,
                UsuarioResponsable = x.UsuarioResponsable != null
                    ? x.UsuarioResponsable.FullName + " · " + x.UsuarioResponsable.Email
                    : null,
                x.Origen,
                x.ImportacionArchivo,
                x.ImportacionHoja,
                x.ImportacionFila,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rawRows.Select(x => new VacacionesKardexReporteRowDto
        {
            MovimientoId = x.MovimientoId,
            EmpleadoId = x.EmpleadoId,
            NumEmpleado = x.NumEmpleado,
            NombreEmpleado = CorporateReportFormatters.CombineFullName(
                x.Nombres,
                x.ApellidoPaterno,
                x.ApellidoMaterno),
            Sucursal = x.Sucursal,
            Departamento = x.Departamento,
            Puesto = x.Puesto,
            EstatusLaboral = x.EstatusLaboral,
            Activo = x.Activo,
            VacacionPeriodoId = x.VacacionPeriodoId,
            AnioServicio = x.AnioServicio,
            PeriodoFechaInicio = x.PeriodoFechaInicio,
            PeriodoFechaFin = x.PeriodoFechaFin,
            TipoMovimiento = x.TipoMovimiento,
            FechaMovimiento = x.FechaMovimiento,
            FechaInicioDisfrute = x.FechaInicioDisfrute,
            FechaFinDisfrute = x.FechaFinDisfrute,
            Dias = x.Dias,
            SaldoAntes = x.SaldoAntes,
            SaldoDespues = x.SaldoDespues,
            Referencia = x.Referencia,
            Comentario = x.Comentario,
            UsuarioResponsableId = x.UsuarioResponsableId,
            UsuarioResponsable = x.UsuarioResponsable,
            Origen = x.Origen,
            ImportacionArchivo = x.ImportacionArchivo,
            ImportacionHoja = x.ImportacionHoja,
            ImportacionFila = x.ImportacionFila,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();
    }

    private async Task<VacacionesSaldosFilterLabels> ResolveFilterLabelsAsync(
        VacacionesSaldosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var labels = new VacacionesSaldosFilterLabels
        {
            Sucursal = "(todas)",
            Departamento = "(todos)",
            Puesto = "(todos)",
            Empleado = "(todos)",
            EstatusLaboral = string.IsNullOrWhiteSpace(query.EstatusLaboral)
                ? "(todos)"
                : CorporateReportFormatters.FormatLabel(query.EstatusLaboral),
            FechaCorte = CorporateReportFormatters.FormatDate(ResolveFechaCorte(query)),
            SoloConSaldo = query.SoloConSaldo.HasValue
                ? CorporateReportFormatters.FormatBool(query.SoloConSaldo.Value)
                : "(todos)",
            SoloVencidos = query.SoloVencidos.HasValue
                ? CorporateReportFormatters.FormatBool(query.SoloVencidos.Value)
                : "(todos)",
            SoloActivos = query.SoloActivos.HasValue
                ? CorporateReportFormatters.FormatBool(query.SoloActivos.Value)
                : "(todos)",
            Search = CorporateReportFormatters.FormatSearchTerm(query.Search)
        };

        if (query.SucursalId.HasValue)
        {
            labels.Sucursal = await _context.Sucursales
                .AsNoTracking()
                .Where(x => x.Id == query.SucursalId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.SucursalId.Value}";
        }

        if (query.DepartamentoId.HasValue)
        {
            labels.Departamento = await _context.Departamentos
                .AsNoTracking()
                .Where(x => x.Id == query.DepartamentoId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.DepartamentoId.Value}";
        }

        if (query.PuestoId.HasValue)
        {
            labels.Puesto = await _context.Puestos
                .AsNoTracking()
                .Where(x => x.Id == query.PuestoId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.PuestoId.Value}";
        }

        if (query.EmpleadoId.HasValue)
        {
            labels.Empleado = await _context.Empleados
                .AsNoTracking()
                .Where(x => x.Id == query.EmpleadoId.Value)
                .Select(x => x.NumEmpleado + " · " + x.Nombres + " " + x.ApellidoPaterno)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.EmpleadoId.Value}";
        }

        return labels;
    }

    private async Task<VacacionesKardexFilterLabels> ResolveKardexFilterLabelsAsync(
        VacacionesKardexReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var labels = new VacacionesKardexFilterLabels
        {
            Sucursal = "(todas)",
            Departamento = "(todos)",
            Puesto = "(todos)",
            Empleado = "(todos)",
            EstatusLaboral = string.IsNullOrWhiteSpace(query.EstatusLaboral)
                ? "(todos)"
                : CorporateReportFormatters.FormatLabel(query.EstatusLaboral),
            TipoMovimiento = string.IsNullOrWhiteSpace(query.TipoMovimiento)
                ? "(todos)"
                : CorporateReportFormatters.FormatLabel(query.TipoMovimiento),
            Origen = string.IsNullOrWhiteSpace(query.Origen)
                ? "(todos)"
                : query.Origen.Trim(),
            FechaDesde = query.FechaDesde.HasValue
                ? CorporateReportFormatters.FormatDate(query.FechaDesde.Value)
                : "(sin inicio)",
            FechaHasta = query.FechaHasta.HasValue
                ? CorporateReportFormatters.FormatDate(query.FechaHasta.Value)
                : "(sin fin)",
            SoloActivos = query.SoloActivos.HasValue
                ? CorporateReportFormatters.FormatBool(query.SoloActivos.Value)
                : "(todos)",
            Search = CorporateReportFormatters.FormatSearchTerm(query.Search)
        };

        if (query.SucursalId.HasValue)
        {
            labels.Sucursal = await _context.Sucursales
                .AsNoTracking()
                .Where(x => x.Id == query.SucursalId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.SucursalId.Value}";
        }

        if (query.DepartamentoId.HasValue)
        {
            labels.Departamento = await _context.Departamentos
                .AsNoTracking()
                .Where(x => x.Id == query.DepartamentoId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.DepartamentoId.Value}";
        }

        if (query.PuestoId.HasValue)
        {
            labels.Puesto = await _context.Puestos
                .AsNoTracking()
                .Where(x => x.Id == query.PuestoId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.PuestoId.Value}";
        }

        if (query.EmpleadoId.HasValue)
        {
            labels.Empleado = await _context.Empleados
                .AsNoTracking()
                .Where(x => x.Id == query.EmpleadoId.Value)
                .Select(x => x.NumEmpleado + " · " + x.Nombres + " " + x.ApellidoPaterno)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.EmpleadoId.Value}";
        }

        return labels;
    }

    private static IReadOnlyList<(string Label, string Value)> BuildFilterItems(
        VacacionesSaldosFilterLabels labels)
    {
        return
        [
            ("Sucursal", labels.Sucursal),
            ("Departamento", labels.Departamento),
            ("Puesto", labels.Puesto),
            ("Empleado", labels.Empleado),
            ("Estatus laboral", labels.EstatusLaboral),
            ("Fecha corte", labels.FechaCorte),
            ("Solo con saldo", labels.SoloConSaldo),
            ("Solo vencidos", labels.SoloVencidos),
            ("Solo activos", labels.SoloActivos),
            ("Búsqueda", labels.Search)
        ];
    }

    private static IReadOnlyList<(string Label, string Value)> BuildKardexFilterItems(
        VacacionesKardexFilterLabels labels)
    {
        return
        [
            ("Sucursal", labels.Sucursal),
            ("Departamento", labels.Departamento),
            ("Puesto", labels.Puesto),
            ("Empleado", labels.Empleado),
            ("Estatus laboral", labels.EstatusLaboral),
            ("Tipo movimiento", labels.TipoMovimiento),
            ("Origen", labels.Origen),
            ("Fecha desde", labels.FechaDesde),
            ("Fecha hasta", labels.FechaHasta),
            ("Solo activos", labels.SoloActivos),
            ("Búsqueda", labels.Search)
        ];
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        VacacionesSaldosReporteResultDto result,
        VacacionesSaldosFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de saldos de vacaciones",
            "Resumen ejecutivo",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 10,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildFilterItems(filterLabels));

        nextRow += 1;

        nextRow = CorporateExcelStyles.WriteKpiCards(
            ws,
            nextRow,
            new (string Title, string Value, string AccentColor)[]
            {
                ("Total registros", result.TotalRegistros.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPrimary),
                ("Empleados con saldo", result.EmpleadosConSaldo.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiSuccess),
                ("Saldo total", FormatDecimal(result.TotalSaldo), CorporateReportPalette.KpiTeal),
                ("Periodos vencidos", result.PeriodosVencidos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.Danger),
                ("Días vencidos", FormatDecimal(result.TotalDiasVencidos), CorporateReportPalette.KpiWarning)
            });

        nextRow += 2;

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 1,
            sectionTitle: "Por estatus laboral",
            header1: "Estatus",
            header2: "Saldo",
            rows: result.Items
                .GroupBy(x => CorporateReportFormatters.FormatLabel(x.EstatusLaboral))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Sum(item => item.Saldo)))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 4,
            sectionTitle: "Por sucursal",
            header1: "Sucursal",
            header2: "Saldo",
            rows: result.Items
                .GroupBy(x => CorporateReportFormatters.NullSafe(x.Sucursal, "(sin sucursal)"))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Sum(item => item.Saldo)))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 7,
            sectionTitle: "Por vencimiento",
            header1: "Estatus",
            header2: "Saldo",
            rows:
            [
                ("Vencidos", result.Items.Where(x => x.EstaVencido).Sum(x => x.Saldo)),
                ("Vigentes", result.Items.Where(x => !x.EstaVencido).Sum(x => x.Saldo))
            ]);

        CorporateExcelStyles.SetColumnWidths(ws, 24, 16, 4, 24, 16, 4, 24, 16, 4, 16);
    }

    private static void BuildKardexResumenSheet(
        XLWorkbook workbook,
        VacacionesKardexReporteResultDto result,
        VacacionesKardexFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de kárdex de vacaciones",
            "Resumen ejecutivo de movimientos",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 10,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildKardexFilterItems(filterLabels));

        nextRow += 1;

        nextRow = CorporateExcelStyles.WriteKpiCards(
            ws,
            nextRow,
            new (string Title, string Value, string AccentColor)[]
            {
                ("Movimientos", result.TotalMovimientos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPrimary),
                ("Días positivos", FormatDecimal(result.TotalDiasPositivos), CorporateReportPalette.KpiSuccess),
                ("Días negativos", FormatDecimal(result.TotalDiasNegativos), CorporateReportPalette.Danger),
                ("Balance", FormatDecimal(result.BalanceDias), CorporateReportPalette.KpiTeal),
                ("Importación", result.MovimientosImportacion.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiWarning)
            });

        nextRow += 2;

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 1,
            sectionTitle: "Por tipo de movimiento",
            header1: "Tipo",
            header2: "Días",
            rows: result.Items
                .GroupBy(x => CorporateReportFormatters.FormatLabel(x.TipoMovimiento))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Sum(item => item.Dias)))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 4,
            sectionTitle: "Por origen",
            header1: "Origen",
            header2: "Días",
            rows: result.Items
                .GroupBy(x => CorporateReportFormatters.NullSafe(x.Origen, "(sin origen)"))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Sum(item => item.Dias)))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 7,
            sectionTitle: "Por sucursal",
            header1: "Sucursal",
            header2: "Días",
            rows: result.Items
                .GroupBy(x => CorporateReportFormatters.NullSafe(x.Sucursal, "(sin sucursal)"))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Sum(item => item.Dias)))
                .ToList());

        CorporateExcelStyles.SetColumnWidths(ws, 24, 16, 4, 24, 16, 4, 24, 16, 4, 16);
    }

    private static void BuildDetalleSheet(
        XLWorkbook workbook,
        IReadOnlyList<VacacionesSaldosReporteRowDto> rows,
        VacacionesSaldosFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Saldos");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de saldos de vacaciones",
            "Detalle operativo",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 19,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildFilterItems(filterLabels));

        nextRow += 2;

        var headerRow = nextRow;

        ws.Cell(headerRow, 1).Value = "Núm. Emp.";
        ws.Cell(headerRow, 2).Value = "Empleado";
        ws.Cell(headerRow, 3).Value = "Sucursal";
        ws.Cell(headerRow, 4).Value = "Departamento";
        ws.Cell(headerRow, 5).Value = "Puesto";
        ws.Cell(headerRow, 6).Value = "Estatus laboral";
        ws.Cell(headerRow, 7).Value = "Activo";
        ws.Cell(headerRow, 8).Value = "Ingreso";
        ws.Cell(headerRow, 9).Value = "Año servicio";
        ws.Cell(headerRow, 10).Value = "Inicio periodo";
        ws.Cell(headerRow, 11).Value = "Fin periodo";
        ws.Cell(headerRow, 12).Value = "Límite disfrute";
        ws.Cell(headerRow, 13).Value = "Derecho";
        ws.Cell(headerRow, 14).Value = "Tomados";
        ws.Cell(headerRow, 15).Value = "Pagados";
        ws.Cell(headerRow, 16).Value = "Ajustados";
        ws.Cell(headerRow, 17).Value = "Vencidos";
        ws.Cell(headerRow, 18).Value = "Saldo";
        ws.Cell(headerRow, 19).Value = "Estatus periodo";

        CorporateExcelStyles.ApplyTableHeader(ws.Range(headerRow, 1, headerRow, 19));

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = CorporateReportFormatters.ExcelText(item.NumEmpleado);
            ws.Cell(rowIndex, 2).Value = CorporateReportFormatters.ExcelText(item.NombreEmpleado);
            ws.Cell(rowIndex, 3).Value = CorporateReportFormatters.ExcelText(item.Sucursal, "(sin sucursal)");
            ws.Cell(rowIndex, 4).Value = CorporateReportFormatters.ExcelText(item.Departamento, "(sin departamento)");
            ws.Cell(rowIndex, 5).Value = CorporateReportFormatters.ExcelText(item.Puesto, "(sin puesto)");
            ws.Cell(rowIndex, 6).Value = CorporateReportFormatters.FormatLabel(item.EstatusLaboral);
            ws.Cell(rowIndex, 7).Value = item.Activo ? "Sí" : "No";
            ws.Cell(rowIndex, 8).Value = CorporateReportFormatters.ExcelDate(item.FechaIngreso);
            ws.Cell(rowIndex, 9).Value = item.AnioServicio;
            ws.Cell(rowIndex, 10).Value = CorporateReportFormatters.ExcelDate(item.FechaInicio);
            ws.Cell(rowIndex, 11).Value = CorporateReportFormatters.ExcelDate(item.FechaFin);
            ws.Cell(rowIndex, 12).Value = CorporateReportFormatters.ExcelDate(item.FechaLimiteDisfrute);
            ws.Cell(rowIndex, 13).Value = item.DiasDerecho;
            ws.Cell(rowIndex, 14).Value = item.DiasTomados;
            ws.Cell(rowIndex, 15).Value = item.DiasPagados;
            ws.Cell(rowIndex, 16).Value = item.DiasAjustados;
            ws.Cell(rowIndex, 17).Value = item.DiasVencidos;
            ws.Cell(rowIndex, 18).Value = item.Saldo;
            ws.Cell(rowIndex, 19).Value = CorporateReportFormatters.FormatLabel(item.EstatusPeriodo);

            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;

        CorporateExcelStyles.ApplyDataGrid(ws, headerRow + 1, lastDataRow, 1, 19);
        CorporateExcelStyles.FinalizeTable(ws, headerRow, 1, 19, lastDataRow);

        if (lastDataRow >= headerRow + 1)
        {
            ws.Range(headerRow + 1, 13, lastDataRow, 18)
                .Style.NumberFormat.Format = "#,##0.00";
        }

        CorporateExcelStyles.SetColumnWidths(
            ws,
            14, 32, 22, 22, 22, 18, 10, 14, 12, 14,
            14, 16, 12, 12, 12, 12, 12, 12, 18);
    }

    private static void BuildKardexDetalleSheet(
        XLWorkbook workbook,
        IReadOnlyList<VacacionesKardexReporteRowDto> rows,
        VacacionesKardexFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Kardex");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de kárdex de vacaciones",
            "Detalle de movimientos",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 24,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildKardexFilterItems(filterLabels));

        nextRow += 2;

        var headerRow = nextRow;

        ws.Cell(headerRow, 1).Value = "Fecha movimiento";
        ws.Cell(headerRow, 2).Value = "Núm. Emp.";
        ws.Cell(headerRow, 3).Value = "Empleado";
        ws.Cell(headerRow, 4).Value = "Sucursal";
        ws.Cell(headerRow, 5).Value = "Departamento";
        ws.Cell(headerRow, 6).Value = "Puesto";
        ws.Cell(headerRow, 7).Value = "Estatus laboral";
        ws.Cell(headerRow, 8).Value = "Activo";
        ws.Cell(headerRow, 9).Value = "Año servicio";
        ws.Cell(headerRow, 10).Value = "Periodo inicio";
        ws.Cell(headerRow, 11).Value = "Periodo fin";
        ws.Cell(headerRow, 12).Value = "Tipo movimiento";
        ws.Cell(headerRow, 13).Value = "Inicio disfrute";
        ws.Cell(headerRow, 14).Value = "Fin disfrute";
        ws.Cell(headerRow, 15).Value = "Días";
        ws.Cell(headerRow, 16).Value = "Saldo antes";
        ws.Cell(headerRow, 17).Value = "Saldo después";
        ws.Cell(headerRow, 18).Value = "Referencia";
        ws.Cell(headerRow, 19).Value = "Comentario";
        ws.Cell(headerRow, 20).Value = "Responsable";
        ws.Cell(headerRow, 21).Value = "Origen";
        ws.Cell(headerRow, 22).Value = "Archivo importación";
        ws.Cell(headerRow, 23).Value = "Hoja";
        ws.Cell(headerRow, 24).Value = "Fila";

        CorporateExcelStyles.ApplyTableHeader(ws.Range(headerRow, 1, headerRow, 24));

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = CorporateReportFormatters.ExcelDate(item.FechaMovimiento);
            ws.Cell(rowIndex, 2).Value = CorporateReportFormatters.ExcelText(item.NumEmpleado);
            ws.Cell(rowIndex, 3).Value = CorporateReportFormatters.ExcelText(item.NombreEmpleado);
            ws.Cell(rowIndex, 4).Value = CorporateReportFormatters.ExcelText(item.Sucursal, "(sin sucursal)");
            ws.Cell(rowIndex, 5).Value = CorporateReportFormatters.ExcelText(item.Departamento, "(sin departamento)");
            ws.Cell(rowIndex, 6).Value = CorporateReportFormatters.ExcelText(item.Puesto, "(sin puesto)");
            ws.Cell(rowIndex, 7).Value = CorporateReportFormatters.FormatLabel(item.EstatusLaboral);
            ws.Cell(rowIndex, 8).Value = item.Activo ? "Sí" : "No";
            ws.Cell(rowIndex, 9).Value = item.AnioServicio;
            ws.Cell(rowIndex, 10).Value = CorporateReportFormatters.ExcelDate(item.PeriodoFechaInicio);
            ws.Cell(rowIndex, 11).Value = CorporateReportFormatters.ExcelDate(item.PeriodoFechaFin);
            ws.Cell(rowIndex, 12).Value = CorporateReportFormatters.FormatLabel(item.TipoMovimiento);
            ws.Cell(rowIndex, 13).Value = CorporateReportFormatters.ExcelDate(item.FechaInicioDisfrute);
            ws.Cell(rowIndex, 14).Value = CorporateReportFormatters.ExcelDate(item.FechaFinDisfrute);
            ws.Cell(rowIndex, 15).Value = item.Dias;
            ws.Cell(rowIndex, 16).Value = item.SaldoAntes;
            ws.Cell(rowIndex, 17).Value = item.SaldoDespues;
            ws.Cell(rowIndex, 18).Value = CorporateReportFormatters.ExcelText(item.Referencia);
            ws.Cell(rowIndex, 19).Value = CorporateReportFormatters.ExcelText(item.Comentario);
            ws.Cell(rowIndex, 20).Value = CorporateReportFormatters.ExcelText(item.UsuarioResponsable);
            ws.Cell(rowIndex, 21).Value = CorporateReportFormatters.ExcelText(item.Origen);
            ws.Cell(rowIndex, 22).Value = CorporateReportFormatters.ExcelText(item.ImportacionArchivo);
            ws.Cell(rowIndex, 23).Value = CorporateReportFormatters.ExcelText(item.ImportacionHoja);
            ws.Cell(rowIndex, 24).Value = item.ImportacionFila.HasValue
                ? item.ImportacionFila.Value.ToString(CultureInfo.InvariantCulture)
                : "—";

            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;

        CorporateExcelStyles.ApplyDataGrid(ws, headerRow + 1, lastDataRow, 1, 24);
        CorporateExcelStyles.FinalizeTable(ws, headerRow, 1, 24, lastDataRow);

        if (lastDataRow >= headerRow + 1)
        {
            ws.Range(headerRow + 1, 15, lastDataRow, 17)
                .Style.NumberFormat.Format = "#,##0.00";
        }

        CorporateExcelStyles.SetColumnWidths(
            ws,
            16, 14, 32, 22, 22, 22, 18, 10, 12, 14, 14, 20,
            14, 14, 12, 12, 14, 24, 40, 30, 18, 28, 18, 10);
    }

    private static void WriteSummaryTable(
        IXLWorksheet ws,
        int startRow,
        int startColumn,
        string sectionTitle,
        string header1,
        string header2,
        IReadOnlyList<(string Label, decimal Value)> rows)
    {
        CorporateExcelStyles.WriteSectionTitle(ws, startRow, startColumn, sectionTitle);

        ws.Cell(startRow + 1, startColumn).Value = header1;
        ws.Cell(startRow + 1, startColumn + 1).Value = header2;

        CorporateExcelStyles.ApplyTableHeader(
            ws.Range(startRow + 1, startColumn, startRow + 1, startColumn + 1));

        var rowIndex = startRow + 2;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, startColumn).Value = item.Label;
            ws.Cell(rowIndex, startColumn + 1).Value = item.Value;
            ws.Cell(rowIndex, startColumn + 1).Style.NumberFormat.Format = "#,##0.00";
            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;
        CorporateExcelStyles.ApplyDataGrid(ws, startRow + 2, lastDataRow, startColumn, startColumn + 1);
    }

    private static void ComposeDistributionSection(
        IContainer container,
        IReadOnlyList<VacacionesSaldosReporteRowDto> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Resumen por grupos", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(12);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);

                    left.Item().Text("Por sucursal")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.NullSafe(x.Sucursal, "(sin sucursal)"))
                        .OrderBy(x => x.Key)
                        .Take(10)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        left.Item().Text("Sin registros para resumir.")
                            .FontSize(8.5f)
                            .FontColor(CorporateReportPalette.Ink500);
                    }
                    else
                    {
                        foreach (var group in groups)
                        {
                            left.Item().Text(text =>
                            {
                                text.Span($"{group.Key}: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                                text.Span($"{FormatDecimal(group.Sum(x => x.Saldo))} días").FontColor(CorporateReportPalette.Ink900);
                            });
                        }
                    }
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);

                    right.Item().Text("Por estatus")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.FormatLabel(x.EstatusLaboral))
                        .OrderBy(x => x.Key)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        right.Item().Text("Sin registros para resumir.")
                            .FontSize(8.5f)
                            .FontColor(CorporateReportPalette.Ink500);
                    }
                    else
                    {
                        foreach (var group in groups)
                        {
                            right.Item().Text(text =>
                            {
                                text.Span($"{group.Key}: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                                text.Span($"{FormatDecimal(group.Sum(x => x.Saldo))} días").FontColor(CorporateReportPalette.Ink900);
                            });
                        }
                    }
                });
            });
        });
    }

    private static void ComposeKardexDistributionSection(
        IContainer container,
        IReadOnlyList<VacacionesKardexReporteRowDto> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Resumen por grupos", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(12);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);
                    left.Item().Text("Por tipo de movimiento")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.FormatLabel(x.TipoMovimiento))
                        .OrderBy(x => x.Key)
                        .Take(10)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        left.Item().Text("Sin movimientos para resumir.")
                            .FontSize(8.5f)
                            .FontColor(CorporateReportPalette.Ink500);
                    }
                    else
                    {
                        foreach (var group in groups)
                        {
                            left.Item().Text(text =>
                            {
                                text.Span($"{group.Key}: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                                text.Span($"{FormatDecimal(group.Sum(x => x.Dias))} días").FontColor(CorporateReportPalette.Ink900);
                            });
                        }
                    }
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);
                    right.Item().Text("Por origen")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.NullSafe(x.Origen, "(sin origen)"))
                        .OrderBy(x => x.Key)
                        .Take(10)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        right.Item().Text("Sin movimientos para resumir.")
                            .FontSize(8.5f)
                            .FontColor(CorporateReportPalette.Ink500);
                    }
                    else
                    {
                        foreach (var group in groups)
                        {
                            right.Item().Text(text =>
                            {
                                text.Span($"{group.Key}: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                                text.Span($"{FormatDecimal(group.Sum(x => x.Dias))} días").FontColor(CorporateReportPalette.Ink900);
                            });
                        }
                    }
                });
            });
        });
    }

    private static void ComposeDetailSection(
        IContainer container,
        IReadOnlyList<VacacionesSaldosReporteRowDto> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Detalle de saldos", body =>
        {
            if (rows.Count == 0)
            {
                body.Item().Element(c =>
                    CorporatePdfBlocks.ComposeEmptyState(c, "No se encontraron saldos con los filtros aplicados."));
                return;
            }

            body.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(68);
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(1.4f);
                    columns.ConstantColumn(58);
                    columns.ConstantColumn(44);
                    columns.ConstantColumn(58);
                    columns.ConstantColumn(48);
                    columns.ConstantColumn(48);
                    columns.ConstantColumn(48);
                    columns.ConstantColumn(48);
                    columns.ConstantColumn(48);
                    columns.ConstantColumn(68);
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Núm. Emp.");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Empleado");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Sucursal");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Depto.");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Ingreso");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Año");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Límite");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Derecho");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Tomados");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Pagados");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Vencidos");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Saldo");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Estatus");
                });

                for (var index = 0; index < rows.Count; index++)
                {
                    var item = rows[index];
                    var background = CorporatePdfStyles.ZebraRow(index);
                    var saldoColor = item.Saldo > 0
                        ? CorporateReportPalette.Success
                        : CorporateReportPalette.Neutral;

                    table.Cell().Element(c => c.TableDataCell(background, emphasize: true))
                        .Text(CorporateReportFormatters.NullSafe(item.NumEmpleado));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.NombreEmpleado));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Sucursal, "(sin sucursal)"));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Departamento, "(sin depto.)"));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDate(item.FechaIngreso));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(item.AnioServicio.ToString(CultureInfo.InvariantCulture));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDate(item.FechaLimiteDisfrute));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(FormatDecimal(item.DiasDerecho));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(FormatDecimal(item.DiasTomados));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(FormatDecimal(item.DiasPagados));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(FormatDecimal(item.DiasVencidos));

                    table.Cell().Element(c => c.TableDataCell(background, emphasize: true))
                        .Text(FormatDecimal(item.Saldo))
                        .FontColor(saldoColor)
                        .SemiBold();

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(item.EstaVencido ? "Vencido" : CorporateReportFormatters.FormatLabel(item.EstatusPeriodo));
                }
            });
        });
    }

    private static void ComposeKardexDetailSection(
        IContainer container,
        IReadOnlyList<VacacionesKardexReporteRowDto> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Detalle de movimientos", body =>
        {
            if (rows.Count == 0)
            {
                body.Item().Element(c =>
                    CorporatePdfBlocks.ComposeEmptyState(c, "No se encontraron movimientos con los filtros aplicados."));
                return;
            }

            body.Item().Column(column =>
            {
                column.Spacing(8);

                for (var index = 0; index < rows.Count; index++)
                {
                    var item = rows[index];

                    column.Item().Element(container =>
                        ComposeKardexMovementCard(container, item, index));
                }
            });
        });
    }

    private static void ComposeKardexMovementCard(
        IContainer container,
        VacacionesKardexReporteRowDto item,
        int index)
    {
        var comentarioLines = SplitKardexDetailLines(item.Comentario);
        var referenciaLines = SplitKardexDetailLines(item.Referencia);

        var hasImportacion =
            !string.IsNullOrWhiteSpace(item.ImportacionArchivo) ||
            !string.IsNullOrWhiteSpace(item.ImportacionHoja) ||
            item.ImportacionFila.HasValue;

        var diasColor = item.Dias < 0
            ? CorporateReportPalette.Danger
            : item.Dias > 0
                ? CorporateReportPalette.Success
                : CorporateReportPalette.Neutral;

        var accentColor = item.Dias < 0
            ? CorporateReportPalette.Danger
            : item.Dias > 0
                ? CorporateReportPalette.Success
                : CorporateReportPalette.BrandPrimary;

        container
            .Border(1)
            .BorderColor(CorporateReportPalette.BorderSoft)
            .CornerRadius(8)
            .Background(index % 2 == 0
                ? CorporateReportPalette.White
                : CorporateReportPalette.SurfaceMuted)
            .Padding(8)
            .Column(card =>
            {
                card.Spacing(7);

                card.Item().Row(row =>
                {
                    row.Spacing(8);

                    row.RelativeItem(2.1f).Column(col =>
                    {
                        col.Spacing(1);

                        col.Item().Text(CorporateReportFormatters.NullSafe(item.NombreEmpleado))
                            .FontSize(10.2f)
                            .Bold()
                            .FontColor(CorporateReportPalette.Ink900);

                        col.Item().Text(text =>
                        {
                            text.Span("Núm. ")
                                .FontSize(7.8f)
                                .FontColor(CorporateReportPalette.Ink500);

                            text.Span(CorporateReportFormatters.NullSafe(item.NumEmpleado))
                                .FontSize(8.1f)
                                .SemiBold()
                                .FontColor(CorporateReportPalette.Ink700);

                            text.Span(" · ")
                                .FontSize(8.1f)
                                .FontColor(CorporateReportPalette.Ink400);

                            text.Span(CorporateReportFormatters.NullSafe(item.Sucursal, "Sin sucursal"))
                                .FontSize(8.1f)
                                .FontColor(CorporateReportPalette.Ink700);

                            text.Span(" · ")
                                .FontSize(8.1f)
                                .FontColor(CorporateReportPalette.Ink400);

                            text.Span(CorporateReportFormatters.NullSafe(item.Departamento, "Sin departamento"))
                                .FontSize(8.1f)
                                .FontColor(CorporateReportPalette.Ink700);
                        });
                    });

                    row.ConstantItem(90).Column(col =>
                    {
                        col.Spacing(1);

                        col.Item().Text("Fecha")
                            .FontSize(7.4f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink500);

                        col.Item().Text(CorporateReportFormatters.FormatDate(item.FechaMovimiento))
                            .FontSize(8.6f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink900);
                    });

                    row.ConstantItem(96).Column(col =>
                    {
                        col.Spacing(1);

                        col.Item().Text("Tipo")
                            .FontSize(7.4f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink500);

                        col.Item().Text(CorporateReportFormatters.FormatLabel(item.TipoMovimiento))
                            .FontSize(8.4f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink900);
                    });

                    row.ConstantItem(62).Column(col =>
                    {
                        col.Spacing(1);

                        col.Item().Text("Días")
                            .FontSize(7.4f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink500);

                        col.Item().Text(FormatDecimal(item.Dias))
                            .FontSize(11f)
                            .Bold()
                            .FontColor(diasColor);
                    });

                    row.ConstantItem(92).Column(col =>
                    {
                        col.Spacing(1);

                        col.Item().Text("Saldo")
                            .FontSize(7.4f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink500);

                        col.Item().Text($"{FormatDecimal(item.SaldoAntes)} → {FormatDecimal(item.SaldoDespues)}")
                            .FontSize(8.7f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink900);
                    });
                });

                card.Item()
                    .LineHorizontal(0.8f)
                    .LineColor(CorporateReportPalette.BorderSoft);

                card.Item().Row(row =>
                {
                    row.Spacing(8);

                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Periodo: ")
                            .FontSize(8f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink700);

                        text.Span($"{CorporateReportFormatters.FormatDate(item.PeriodoFechaInicio)} al {CorporateReportFormatters.FormatDate(item.PeriodoFechaFin)}")
                            .FontSize(8f)
                            .FontColor(CorporateReportPalette.Ink900);

                        text.Span(" · Año ")
                            .FontSize(8f)
                            .FontColor(CorporateReportPalette.Ink500);

                        text.Span(item.AnioServicio.ToString(CultureInfo.InvariantCulture))
                            .FontSize(8f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink900);
                    });

                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Origen: ")
                            .FontSize(8f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink700);

                        text.Span(CorporateReportFormatters.NullSafe(item.Origen, "—"))
                            .FontSize(8f)
                            .FontColor(CorporateReportPalette.Ink900);
                    });

                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Responsable: ")
                            .FontSize(8f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.Ink700);

                        text.Span(CorporateReportFormatters.NullSafe(item.UsuarioResponsable, "—"))
                            .FontSize(8f)
                            .FontColor(CorporateReportPalette.Ink900);
                    });
                });

                if (comentarioLines.Count > 0)
                {
                    card.Item()
                        .EnsureSpace(140)
                        .Background(CorporateReportPalette.SurfaceAlt)
                        .Border(1)
                        .BorderColor(CorporateReportPalette.BorderSoft)
                        .CornerRadius(6)
                        .Padding(6)
                        .Column(detail =>
                        {
                            detail.Spacing(2);

                            detail.Item().Text("Detalle histórico")
                                .FontSize(8.5f)
                                .SemiBold()
                                .FontColor(accentColor);

                            for (var lineIndex = 0; lineIndex < comentarioLines.Count; lineIndex++)
                            {
                                var line = comentarioLines[lineIndex];

                                detail.Item().Row(lineRow =>
                                {
                                    lineRow.ConstantItem(22)
                                        .Text($"{lineIndex + 1}.")
                                        .FontSize(7.4f)
                                        .SemiBold()
                                        .FontColor(CorporateReportPalette.BrandPrimary);

                                    lineRow.RelativeItem()
                                        .Text(line)
                                        .FontSize(7.6f)
                                        .FontColor(CorporateReportPalette.Ink900);
                                });
                            }
                        });
                }

                if (referenciaLines.Count > 0)
                {
                    card.Item()
                        .Background(CorporateReportPalette.White)
                        .Border(1)
                        .BorderColor(CorporateReportPalette.BorderSoft)
                        .CornerRadius(6)
                        .Padding(7)
                        .Column(detail =>
                        {
                            detail.Spacing(2);

                            detail.Item().Text("Referencia")
                                .FontSize(8.2f)
                                .SemiBold()
                                .FontColor(CorporateReportPalette.Ink700);

                            for (var lineIndex = 0; lineIndex < referenciaLines.Count; lineIndex++)
                            {
                                var line = referenciaLines[lineIndex];

                                detail.Item().Row(lineRow =>
                                {
                                    lineRow.ConstantItem(22)
                                        .Text($"{lineIndex + 1}.")
                                        .FontSize(7.6f)
                                        .SemiBold()
                                        .FontColor(CorporateReportPalette.BrandPrimary);

                                    lineRow.RelativeItem()
                                        .Text(line)
                                        .FontSize(7.9f)
                                        .FontColor(CorporateReportPalette.Ink700);
                                });
                            }
                        });
                }

                if (hasImportacion)
                {
                    card.Item()
                        .Text(text =>
                        {
                            text.Span("Importación: ")
                                .FontSize(7.9f)
                                .SemiBold()
                                .FontColor(CorporateReportPalette.Ink700);

                            text.Span(CorporateReportFormatters.NullSafe(item.ImportacionArchivo, "—"))
                                .FontSize(7.9f)
                                .FontColor(CorporateReportPalette.Ink700);

                            if (!string.IsNullOrWhiteSpace(item.ImportacionHoja))
                            {
                                text.Span($" · {item.ImportacionHoja.Trim()}")
                                    .FontSize(7.9f)
                                    .FontColor(CorporateReportPalette.Ink700);
                            }

                            if (item.ImportacionFila.HasValue)
                            {
                                text.Span($" · fila {item.ImportacionFila.Value.ToString(CultureInfo.InvariantCulture)}")
                                    .FontSize(7.9f)
                                    .FontColor(CorporateReportPalette.Ink700);
                            }
                        });
                }
            });
    }

    private static IReadOnlyList<string> SplitKardexDetailLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
    }

    private static DateOnly ResolveFechaCorte(VacacionesSaldosReporteQueryDto query)
    {
        return query.FechaCorte ?? DateOnly.FromDateTime(DateTime.Today);
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("#,##0.##", CultureInfo.InvariantCulture);
    }

    private static string BuildFileName(
        VacacionesSaldosReporteQueryDto query,
        string extension)
    {
        return CorporateReportFormatters.BuildTimestampedFileName(
            "vacaciones_saldos",
            extension,
            query.SucursalId.HasValue ? $"sucursal_{query.SucursalId.Value}" : null,
            query.DepartamentoId.HasValue ? $"departamento_{query.DepartamentoId.Value}" : null,
            query.EmpleadoId.HasValue ? $"empleado_{query.EmpleadoId.Value}" : null,
            query.SoloConSaldo == true ? "con_saldo" : null,
            query.SoloVencidos == true ? "vencidos" : null);
    }

    private static string BuildKardexFileName(
        VacacionesKardexReporteQueryDto query,
        string extension)
    {
        return CorporateReportFormatters.BuildTimestampedFileName(
            "vacaciones_kardex",
            extension,
            query.SucursalId.HasValue ? $"sucursal_{query.SucursalId.Value}" : null,
            query.DepartamentoId.HasValue ? $"departamento_{query.DepartamentoId.Value}" : null,
            query.EmpleadoId.HasValue ? $"empleado_{query.EmpleadoId.Value}" : null,
            !string.IsNullOrWhiteSpace(query.TipoMovimiento) ? CorporateReportFormatters.FormatLabel(query.TipoMovimiento) : null,
            !string.IsNullOrWhiteSpace(query.Origen) ? query.Origen.Trim() : null);
    }

    private sealed class VacacionesSaldosFilterLabels
    {
        public string Sucursal { get; set; } = "(todas)";
        public string Departamento { get; set; } = "(todos)";
        public string Puesto { get; set; } = "(todos)";
        public string Empleado { get; set; } = "(todos)";
        public string EstatusLaboral { get; set; } = "(todos)";
        public string FechaCorte { get; set; } = "(hoy)";
        public string SoloConSaldo { get; set; } = "(todos)";
        public string SoloVencidos { get; set; } = "(todos)";
        public string SoloActivos { get; set; } = "(todos)";
        public string Search { get; set; } = "(vacío)";
    }

    private sealed class VacacionesKardexFilterLabels
    {
        public string Sucursal { get; set; } = "(todas)";
        public string Departamento { get; set; } = "(todos)";
        public string Puesto { get; set; } = "(todos)";
        public string Empleado { get; set; } = "(todos)";
        public string EstatusLaboral { get; set; } = "(todos)";
        public string TipoMovimiento { get; set; } = "(todos)";
        public string Origen { get; set; } = "(todos)";
        public string FechaDesde { get; set; } = "(sin inicio)";
        public string FechaHasta { get; set; } = "(sin fin)";
        public string SoloActivos { get; set; } = "(todos)";
        public string Search { get; set; } = "(vacío)";
    }
}




