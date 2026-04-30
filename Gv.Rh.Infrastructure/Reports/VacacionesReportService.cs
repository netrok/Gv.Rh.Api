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
            14, // Núm Emp
            32, // Empleado
            22, // Sucursal
            22, // Departamento
            22, // Puesto
            18, // Estatus laboral
            10, // Activo
            14, // Ingreso
            12, // Año servicio
            14, // Inicio
            14, // Fin
            16, // Límite
            12, // Derecho
            12, // Tomados
            12, // Pagados
            12, // Ajustados
            12, // Vencidos
            12, // Saldo
            18  // Estatus periodo
        );
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
                    columns.ConstantColumn(68);    // Num Emp
                    columns.RelativeColumn(2.4f);  // Empleado
                    columns.RelativeColumn(1.4f);  // Sucursal
                    columns.RelativeColumn(1.4f);  // Departamento
                    columns.ConstantColumn(58);    // Ingreso
                    columns.ConstantColumn(44);    // Año
                    columns.ConstantColumn(58);    // Límite
                    columns.ConstantColumn(48);    // Derecho
                    columns.ConstantColumn(48);    // Tomados
                    columns.ConstantColumn(48);    // Pagados
                    columns.ConstantColumn(48);    // Vencidos
                    columns.ConstantColumn(48);    // Saldo
                    columns.ConstantColumn(68);    // Estatus
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
}