using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Incidencias;
using Gv.Rh.Domain.Common.Enums;
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

public sealed class IncidenciasReportService : IIncidenciasReportService
{
    private readonly RhDbContext _context;

    public IncidenciasReportService(RhDbContext context)
    {
        _context = context;
    }

    public async Task<ReportFileDto> BuildXlsxAsync(
        IncidenciaQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetIncidenciasExportRowsAsync(query, cancellationToken);
        var filterLabels = await ResolveFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        using var workbook = new XLWorkbook();
        CorporateExcelStyles.ApplyWorkbookDefaults(workbook);

        BuildResumenSheet(workbook, rows, filterLabels, generatedAtUtc);
        BuildDetalleSheet(workbook, rows, filterLabels, generatedAtUtc);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ReportFileDto
        {
            Content = stream.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = BuildFileName(query, "xlsx")
        };
    }

    public async Task<ReportFileDto> BuildPdfAsync(
        IncidenciaQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetIncidenciasExportRowsAsync(query, cancellationToken);
        var filterLabels = await ResolveFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        var total = rows.Count;
        var pendientes = rows.Count(x => NormalizeKey(x.Estatus) == nameof(EstatusIncidencia.PENDIENTE));
        var aprobadas = rows.Count(x => NormalizeKey(x.Estatus) == nameof(EstatusIncidencia.APROBADA));
        var rechazadas = rows.Count(x => NormalizeKey(x.Estatus) == nameof(EstatusIncidencia.RECHAZADA));
        var empleadosUnicos = rows
            .Select(x => x.EmpleadoId)
            .Where(x => x > 0)
            .Distinct()
            .Count();

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
                        "Reporte de incidencias",
                        "Resumen ejecutivo y detalle operativo",
                        generatedAtUtc));

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
                            ("Total de incidencias", total.ToString(CultureInfo.InvariantCulture), "Registros visibles en el reporte", CorporateReportPalette.KpiPrimary),
                            ("Pendientes", pendientes.ToString(CultureInfo.InvariantCulture), "Requieren atención", CorporateReportPalette.KpiWarning),
                            ("Aprobadas", aprobadas.ToString(CultureInfo.InvariantCulture), "Resolución favorable", CorporateReportPalette.KpiSuccess),
                            ("Rechazadas", rechazadas.ToString(CultureInfo.InvariantCulture), "Resolución no favorable", CorporateReportPalette.Danger),
                            ("Empleados únicos", empleadosUnicos.ToString(CultureInfo.InvariantCulture), "Personal involucrado", CorporateReportPalette.KpiTeal)));

                    column.Item().Element(container => ComposeDistributionSection(container, rows));
                    column.Item().Element(container => ComposeDetailSection(container, rows));
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

    private IQueryable<Incidencia> BuildBaseQuery(IncidenciaQueryDto query)
    {
        var incidenciasQuery = _context.Incidencias
            .AsNoTracking()
            .Include(x => x.Empleado)
            .Include(x => x.Sucursal)
            .AsQueryable();

        if (query.EmpleadoId.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.EmpleadoId == query.EmpleadoId.Value);

        if (query.SucursalId.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.SucursalId == query.SucursalId.Value);

        if (query.Tipo.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.Tipo == query.Tipo.Value);

        if (query.Estatus.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.Estatus == query.Estatus.Value);

        if (query.SoloPendientes)
            incidenciasQuery = incidenciasQuery.Where(x => x.Estatus == EstatusIncidencia.PENDIENTE);

        if (query.FechaDesde.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.FechaInicio >= query.FechaDesde.Value);

        if (query.FechaHasta.HasValue)
            incidenciasQuery = incidenciasQuery.Where(x => x.FechaFin <= query.FechaHasta.Value);

        return incidenciasQuery;
    }

    private async Task<List<IncidenciaExportRowDto>> GetIncidenciasExportRowsAsync(
        IncidenciaQueryDto query,
        CancellationToken cancellationToken)
    {
        var rawRows = await BuildBaseQuery(query)
            .OrderByDescending(x => x.FechaInicio)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.EmpleadoId,
                NumEmpleado = x.Empleado.NumEmpleado,
                x.Empleado.Nombres,
                x.Empleado.ApellidoPaterno,
                x.Empleado.ApellidoMaterno,
                Sucursal = x.Sucursal != null ? x.Sucursal.Nombre : null,
                x.Tipo,
                x.Estatus,
                x.FechaInicio,
                x.FechaFin,
                x.Comentario,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rawRows.Select(x => new IncidenciaExportRowDto
        {
            Id = x.Id,
            EmpleadoId = x.EmpleadoId,
            NumEmpleado = x.NumEmpleado,
            Empleado = CorporateReportFormatters.CombineFullName(
                x.Nombres,
                x.ApellidoPaterno,
                x.ApellidoMaterno),
            Sucursal = x.Sucursal,
            Tipo = x.Tipo.ToString(),
            Estatus = x.Estatus.ToString(),
            FechaInicio = x.FechaInicio,
            FechaFin = x.FechaFin,
            Comentario = x.Comentario,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();
    }

    private async Task<IncidenciasReportFilterLabels> ResolveFilterLabelsAsync(
        IncidenciaQueryDto query,
        CancellationToken cancellationToken)
    {
        var labels = new IncidenciasReportFilterLabels
        {
            Empleado = "(todos)",
            Sucursal = "(todas)",
            Tipo = query.Tipo.HasValue
                ? CorporateReportFormatters.FormatLabel(query.Tipo.Value.ToString())
                : "(todos)",
            Estatus = query.Estatus.HasValue
                ? CorporateReportFormatters.FormatLabel(query.Estatus.Value.ToString())
                : "(todos)",
            FechaDesde = CorporateReportFormatters.FormatDateNullable(query.FechaDesde, fallback: "(sin límite)"),
            FechaHasta = CorporateReportFormatters.FormatDateNullable(query.FechaHasta, fallback: "(sin límite)"),
            Periodo = CorporateReportFormatters.FormatDateRange(query.FechaDesde, query.FechaHasta),
            SoloPendientes = CorporateReportFormatters.FormatBool(query.SoloPendientes)
        };

        if (query.EmpleadoId.HasValue)
        {
            var empleado = await _context.Empleados
                .AsNoTracking()
                .Where(x => x.Id == query.EmpleadoId.Value)
                .Select(x => new
                {
                    x.NumEmpleado,
                    x.Nombres,
                    x.ApellidoPaterno,
                    x.ApellidoMaterno
                })
                .FirstOrDefaultAsync(cancellationToken);

            labels.Empleado = empleado is null
                ? $"#{query.EmpleadoId.Value}"
                : $"{CorporateReportFormatters.NullSafe(empleado.NumEmpleado)} · {CorporateReportFormatters.CombineFullName(empleado.Nombres, empleado.ApellidoPaterno, empleado.ApellidoMaterno)}";
        }

        if (query.SucursalId.HasValue)
        {
            labels.Sucursal = await _context.Sucursales
                .AsNoTracking()
                .Where(x => x.Id == query.SucursalId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.SucursalId.Value}";
        }

        return labels;
    }

    private static IReadOnlyList<(string Label, string Value)> BuildFilterItems(IncidenciasReportFilterLabels labels)
    {
        return
        [
            ("Empleado", labels.Empleado),
            ("Sucursal", labels.Sucursal),
            ("Tipo", labels.Tipo),
            ("Estatus", labels.Estatus),
            ("Periodo", labels.Periodo),
            ("Solo pendientes", labels.SoloPendientes)
        ];
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        IReadOnlyList<IncidenciaExportRowDto> rows,
        IncidenciasReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de incidencias",
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

        var total = rows.Count;
        var pendientes = rows.Count(x => NormalizeKey(x.Estatus) == nameof(EstatusIncidencia.PENDIENTE));
        var aprobadas = rows.Count(x => NormalizeKey(x.Estatus) == nameof(EstatusIncidencia.APROBADA));
        var rechazadas = rows.Count(x => NormalizeKey(x.Estatus) == nameof(EstatusIncidencia.RECHAZADA));
        var empleadosUnicos = rows
            .Select(x => x.EmpleadoId)
            .Where(x => x > 0)
            .Distinct()
            .Count();

        nextRow = CorporateExcelStyles.WriteKpiCards(
            ws,
            nextRow,
            new (string Title, string Value, string AccentColor)[]
            {
                ("Total de incidencias", total.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPrimary),
                ("Pendientes", pendientes.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiWarning),
                ("Aprobadas", aprobadas.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiSuccess),
                ("Rechazadas", rechazadas.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.Danger),
                ("Empleados únicos", empleadosUnicos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiTeal)
            });

        nextRow += 2;

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 1,
            sectionTitle: "Por estatus",
            header1: "Estatus",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.FormatLabel(x.Estatus))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 4,
            sectionTitle: "Por tipo",
            header1: "Tipo",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.FormatLabel(x.Tipo))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 7,
            sectionTitle: "Por sucursal",
            header1: "Sucursal",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.NullSafe(x.Sucursal, "(sin sucursal)"))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .ToList());

        CorporateExcelStyles.SetColumnWidths(ws, 22, 18, 4, 22, 18, 4, 24, 18, 4, 16);
    }

    private static void BuildDetalleSheet(
        XLWorkbook workbook,
        IReadOnlyList<IncidenciaExportRowDto> rows,
        IncidenciasReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Incidencias");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de incidencias",
            "Detalle operativo",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 10,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildFilterItems(filterLabels));

        nextRow += 2;

        var headerRow = nextRow;

        ws.Cell(headerRow, 1).Value = "ID";
        ws.Cell(headerRow, 2).Value = "Núm. Emp.";
        ws.Cell(headerRow, 3).Value = "Empleado";
        ws.Cell(headerRow, 4).Value = "Sucursal";
        ws.Cell(headerRow, 5).Value = "Tipo";
        ws.Cell(headerRow, 6).Value = "Estatus";
        ws.Cell(headerRow, 7).Value = "Inicio";
        ws.Cell(headerRow, 8).Value = "Fin";
        ws.Cell(headerRow, 9).Value = "Comentario";
        ws.Cell(headerRow, 10).Value = "Creado UTC";

        CorporateExcelStyles.ApplyTableHeader(ws.Range(headerRow, 1, headerRow, 10));

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = item.Id;
            ws.Cell(rowIndex, 2).Value = CorporateReportFormatters.ExcelText(item.NumEmpleado);
            ws.Cell(rowIndex, 3).Value = CorporateReportFormatters.ExcelText(item.Empleado);
            ws.Cell(rowIndex, 4).Value = CorporateReportFormatters.ExcelText(item.Sucursal, "(sin sucursal)");
            ws.Cell(rowIndex, 5).Value = CorporateReportFormatters.FormatLabel(item.Tipo);
            ws.Cell(rowIndex, 6).Value = CorporateReportFormatters.FormatLabel(item.Estatus);
            ws.Cell(rowIndex, 7).Value = CorporateReportFormatters.ExcelDate(item.FechaInicio);
            ws.Cell(rowIndex, 8).Value = CorporateReportFormatters.ExcelDate(item.FechaFin);
            ws.Cell(rowIndex, 9).Value = CorporateReportFormatters.ExcelText(item.Comentario);
            ws.Cell(rowIndex, 10).Value = CorporateReportFormatters.ExcelDateTime(item.CreatedAtUtc);

            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;

        CorporateExcelStyles.ApplyDataGrid(ws, headerRow + 1, lastDataRow, 1, 10);
        CorporateExcelStyles.FinalizeTable(ws, headerRow, 1, 10, lastDataRow);

        CorporateExcelStyles.SetColumnWidths(ws,
            10, // ID
            14, // NumEmp
            30, // Empleado
            22, // Sucursal
            18, // Tipo
            16, // Estatus
            14, // Inicio
            14, // Fin
            36, // Comentario
            22  // Creado UTC
        );
    }

    private static void WriteSummaryTable(
        IXLWorksheet ws,
        int startRow,
        int startColumn,
        string sectionTitle,
        string header1,
        string header2,
        IReadOnlyList<(string Label, int Count)> rows)
    {
        CorporateExcelStyles.WriteSectionTitle(ws, startRow, startColumn, sectionTitle);

        ws.Cell(startRow + 1, startColumn).Value = header1;
        ws.Cell(startRow + 1, startColumn + 1).Value = header2;

        CorporateExcelStyles.ApplyTableHeader(ws.Range(startRow + 1, startColumn, startRow + 1, startColumn + 1));

        var rowIndex = startRow + 2;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, startColumn).Value = item.Label;
            ws.Cell(rowIndex, startColumn + 1).Value = item.Count;
            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;
        CorporateExcelStyles.ApplyDataGrid(ws, startRow + 2, lastDataRow, startColumn, startColumn + 1);
    }

    private static void ComposeDistributionSection(
        IContainer container,
        IReadOnlyList<IncidenciaExportRowDto> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Distribución", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(12);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);

                    left.Item().Text("Por estatus")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.FormatLabel(x.Estatus))
                        .OrderBy(x => x.Key)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        left.Item().Text("Sin incidencias para resumir.")
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
                                text.Span(group.Count().ToString(CultureInfo.InvariantCulture)).FontColor(CorporateReportPalette.Ink900);
                            });
                        }
                    }
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);

                    right.Item().Text("Por tipo")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.FormatLabel(x.Tipo))
                        .OrderBy(x => x.Key)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        right.Item().Text("Sin incidencias para resumir.")
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
                                text.Span(group.Count().ToString(CultureInfo.InvariantCulture)).FontColor(CorporateReportPalette.Ink900);
                            });
                        }
                    }
                });
            });
        });
    }

    private static void ComposeDetailSection(
        IContainer container,
        IReadOnlyList<IncidenciaExportRowDto> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Detalle de incidencias", body =>
        {
            if (rows.Count == 0)
            {
                body.Item().Element(c =>
                    CorporatePdfBlocks.ComposeEmptyState(c, "No se encontraron incidencias con los filtros aplicados."));
                return;
            }

            body.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(28);     // ID
                    columns.ConstantColumn(64);     // NumEmp
                    columns.RelativeColumn(2.8f);   // Empleado
                    columns.RelativeColumn(1.9f);   // Sucursal
                    columns.RelativeColumn(1.4f);   // Tipo
                    columns.RelativeColumn(1.3f);   // Estatus
                    columns.ConstantColumn(58);     // Inicio
                    columns.ConstantColumn(58);     // Fin
                    columns.RelativeColumn(2.4f);   // Comentario
                    columns.ConstantColumn(78);     // Creado UTC
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => c.TableHeaderCell()).Text("ID");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Núm. Emp.");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Empleado");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Sucursal");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Tipo");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Estatus");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Inicio");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Fin");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Comentario");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Creado UTC");
                });

                for (var index = 0; index < rows.Count; index++)
                {
                    var item = rows[index];
                    var background = CorporatePdfStyles.ZebraRow(index);

                    table.Cell().Element(c => c.TableDataCell(background, emphasize: true))
                        .Text(item.Id.ToString(CultureInfo.InvariantCulture));

                    table.Cell().Element(c => c.TableDataCell(background, emphasize: true))
                        .Text(CorporateReportFormatters.NullSafe(item.NumEmpleado));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Empleado));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Sucursal, "(sin sucursal)"));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatLabel(item.Tipo));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatLabel(item.Estatus));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDate(item.FechaInicio));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDate(item.FechaFin));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Comentario));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDateTime(item.CreatedAtUtc));
                }
            });
        });
    }

    private static string BuildFileName(IncidenciaQueryDto query, string extension)
    {
        return CorporateReportFormatters.BuildTimestampedFileName(
            "incidencias",
            extension,
            query.SoloPendientes ? "pendientes" : null,
            query.SucursalId.HasValue ? $"sucursal_{query.SucursalId.Value}" : null,
            query.EmpleadoId.HasValue ? $"empleado_{query.EmpleadoId.Value}" : null);
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToUpperInvariant();
    }

    private sealed class IncidenciasReportFilterLabels
    {
        public string Empleado { get; set; } = "(todos)";
        public string Sucursal { get; set; } = "(todas)";
        public string Tipo { get; set; } = "(todos)";
        public string Estatus { get; set; } = "(todos)";
        public string FechaDesde { get; set; } = "(sin límite)";
        public string FechaHasta { get; set; } = "(sin límite)";
        public string Periodo { get; set; } = "(sin límite)";
        public string SoloPendientes { get; set; } = "No";
    }
}