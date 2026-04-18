using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Sucursales;
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

public sealed class SucursalesReportService : ISucursalesReportService
{
    private readonly RhDbContext _context;

    public SucursalesReportService(RhDbContext context)
    {
        _context = context;
    }

    public async Task<ReportFileDto> BuildXlsxAsync(
        SucursalReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetSucursalesExportRowsAsync(query, cancellationToken);
        var filterLabels = ResolveFilterLabels(query);
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
        SucursalReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetSucursalesExportRowsAsync(query, cancellationToken);
        var filterLabels = ResolveFilterLabels(query);
        var generatedAtUtc = DateTime.UtcNow;

        var total = rows.Count;
        var activas = rows.Count(x => x.Activo);
        var inactivas = total - activas;
        var empleadosActivos = rows.Sum(x => x.EmpleadosActivos);

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
                        "Reporte de sucursales",
                        "Listado corporativo",
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
                            ("Total", total.ToString(CultureInfo.InvariantCulture), "Sucursales visibles", CorporateReportPalette.KpiPrimary),
                            ("Activas", activas.ToString(CultureInfo.InvariantCulture), "Disponibles para operación", CorporateReportPalette.KpiSuccess),
                            ("Inactivas", inactivas.ToString(CultureInfo.InvariantCulture), "Fuera de operación", CorporateReportPalette.KpiWarning),
                            ("Empleados activos", empleadosActivos.ToString(CultureInfo.InvariantCulture), "Plantilla acumulada", CorporateReportPalette.KpiTeal)));

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

    private IQueryable<Sucursal> BuildBaseQuery(SucursalReporteQueryDto query)
    {
        var sucursalesQuery = _context.Sucursales
            .AsNoTracking()
            .Include(x => x.Empleados)
            .AsQueryable();

        if (query.Activo.HasValue)
            sucursalesQuery = sucursalesQuery.Where(x => x.Activo == query.Activo.Value);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();

            sucursalesQuery = sucursalesQuery.Where(x =>
                EF.Functions.ILike(x.Clave, $"%{term}%") ||
                EF.Functions.ILike(x.Nombre, $"%{term}%") ||
                (x.Direccion != null && EF.Functions.ILike(x.Direccion, $"%{term}%")) ||
                (x.Telefono != null && EF.Functions.ILike(x.Telefono, $"%{term}%")));
        }

        return sucursalesQuery;
    }

    private async Task<List<SucursalExportRow>> GetSucursalesExportRowsAsync(
        SucursalReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rawRows = await BuildBaseQuery(query)
            .OrderBy(x => x.Nombre)
            .Select(x => new
            {
                x.Id,
                x.Clave,
                x.Nombre,
                x.Direccion,
                x.Telefono,
                x.Activo,
                EmpleadosActivos = x.Empleados.Count(e => e.Activo)
            })
            .ToListAsync(cancellationToken);

        return rawRows.Select(x => new SucursalExportRow
        {
            Id = x.Id,
            Clave = x.Clave,
            Nombre = x.Nombre,
            Direccion = x.Direccion,
            Telefono = x.Telefono,
            Activo = x.Activo,
            ActivoLabel = CorporateReportFormatters.FormatBool(x.Activo),
            EmpleadosActivos = x.EmpleadosActivos
        }).ToList();
    }

    private static SucursalReportFilterLabels ResolveFilterLabels(SucursalReporteQueryDto query)
    {
        return new SucursalReportFilterLabels
        {
            Activo = query.Activo.HasValue
                ? CorporateReportFormatters.FormatBool(query.Activo.Value)
                : "(todos)",
            Search = CorporateReportFormatters.FormatSearchTerm(query.Q)
        };
    }

    private static IReadOnlyList<(string Label, string Value)> BuildFilterItems(SucursalReportFilterLabels labels)
    {
        return
        [
            ("Activo", labels.Activo),
            ("Búsqueda", labels.Search)
        ];
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        IReadOnlyList<SucursalExportRow> rows,
        SucursalReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de sucursales",
            "Resumen ejecutivo",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 9,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildFilterItems(filterLabels));

        nextRow += 1;

        var total = rows.Count;
        var activas = rows.Count(x => x.Activo);
        var inactivas = total - activas;
        var empleadosActivos = rows.Sum(x => x.EmpleadosActivos);

        nextRow = CorporateExcelStyles.WriteKpiCards(
            ws,
            nextRow,
            new (string Title, string Value, string AccentColor)[]
            {
                ("Total", total.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPrimary),
                ("Activas", activas.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiSuccess),
                ("Inactivas", inactivas.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiWarning),
                ("Empleados activos", empleadosActivos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiTeal)
            },
            startColumn: 1,
            cardWidth: 2,
            gap: 1);

        nextRow += 2;

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 1,
            sectionTitle: "Por estado",
            header1: "Estado",
            header2: "Cantidad",
            rows:
            [
                ("Activas", activas),
                ("Inactivas", inactivas)
            ]);

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 4,
            sectionTitle: "Top sucursales por empleados activos",
            header1: "Sucursal",
            header2: "Activos",
            rows: rows
                .OrderByDescending(x => x.EmpleadosActivos)
                .ThenBy(x => x.Nombre)
                .Take(12)
                .Select(x => (x.Nombre, x.EmpleadosActivos))
                .ToList());

        CorporateExcelStyles.SetColumnWidths(ws, 24, 18, 4, 30, 18, 4, 16, 16, 16);
    }

    private static void BuildDetalleSheet(
        XLWorkbook workbook,
        IReadOnlyList<SucursalExportRow> rows,
        SucursalReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Sucursales");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de sucursales",
            "Detalle operativo",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 7,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildFilterItems(filterLabels));

        nextRow += 2;

        var headerRow = nextRow;

        ws.Cell(headerRow, 1).Value = "Clave";
        ws.Cell(headerRow, 2).Value = "Nombre";
        ws.Cell(headerRow, 3).Value = "Dirección";
        ws.Cell(headerRow, 4).Value = "Teléfono";
        ws.Cell(headerRow, 5).Value = "Activo";
        ws.Cell(headerRow, 6).Value = "Empleados activos";
        ws.Cell(headerRow, 7).Value = "Id";

        CorporateExcelStyles.ApplyTableHeader(ws.Range(headerRow, 1, headerRow, 7));

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = CorporateReportFormatters.ExcelText(item.Clave);
            ws.Cell(rowIndex, 2).Value = CorporateReportFormatters.ExcelText(item.Nombre);
            ws.Cell(rowIndex, 3).Value = CorporateReportFormatters.ExcelText(item.Direccion);
            ws.Cell(rowIndex, 4).Value = CorporateReportFormatters.ExcelText(item.Telefono);
            ws.Cell(rowIndex, 5).Value = item.ActivoLabel;
            ws.Cell(rowIndex, 6).Value = item.EmpleadosActivos;
            ws.Cell(rowIndex, 7).Value = item.Id;

            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;

        CorporateExcelStyles.ApplyDataGrid(ws, headerRow + 1, lastDataRow, 1, 7);
        CorporateExcelStyles.FinalizeTable(ws, headerRow, 1, 7, lastDataRow);

        CorporateExcelStyles.SetColumnWidths(ws,
            16, // Clave
            26, // Nombre
            34, // Dirección
            18, // Teléfono
            12, // Activo
            18, // Empleados activos
            10  // Id
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
        IReadOnlyList<SucursalExportRow> rows)
    {
        var activas = rows.Count(x => x.Activo);
        var inactivas = rows.Count - activas;

        CorporatePdfBlocks.ComposeSection(container, "Distribución", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(12);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);

                    left.Item().Text("Por estado")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    left.Item().Text(text =>
                    {
                        text.Span("Activas: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                        text.Span(activas.ToString(CultureInfo.InvariantCulture)).FontColor(CorporateReportPalette.Ink900);
                    });

                    left.Item().Text(text =>
                    {
                        text.Span("Inactivas: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                        text.Span(inactivas.ToString(CultureInfo.InvariantCulture)).FontColor(CorporateReportPalette.Ink900);
                    });
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);

                    right.Item().Text("Top por empleados activos")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var items = rows
                        .OrderByDescending(x => x.EmpleadosActivos)
                        .ThenBy(x => x.Nombre)
                        .Take(8)
                        .ToList();

                    if (items.Count == 0)
                    {
                        right.Item().Text("Sin sucursales para resumir.")
                            .FontSize(8.5f)
                            .FontColor(CorporateReportPalette.Ink500);
                    }
                    else
                    {
                        foreach (var item in items)
                        {
                            right.Item().Text(text =>
                            {
                                text.Span($"{item.Nombre}: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                                text.Span(item.EmpleadosActivos.ToString(CultureInfo.InvariantCulture)).FontColor(CorporateReportPalette.Ink900);
                            });
                        }
                    }
                });
            });
        });
    }

    private static void ComposeDetailSection(
        IContainer container,
        IReadOnlyList<SucursalExportRow> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Detalle de sucursales", body =>
        {
            if (rows.Count == 0)
            {
                body.Item().Element(c =>
                    CorporatePdfBlocks.ComposeEmptyState(c, "No se encontraron sucursales con los filtros aplicados."));
                return;
            }

            body.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(70);     // Clave
                    columns.RelativeColumn(2.0f);   // Nombre
                    columns.RelativeColumn(2.8f);   // Dirección
                    columns.ConstantColumn(92);     // Teléfono
                    columns.ConstantColumn(54);     // Activo
                    columns.ConstantColumn(74);     // Empleados activos
                    columns.ConstantColumn(34);     // Id
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Clave");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Nombre");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Dirección");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Teléfono");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Activo");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Emp. activos");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Id");
                });

                for (var index = 0; index < rows.Count; index++)
                {
                    var item = rows[index];
                    var background = CorporatePdfStyles.ZebraRow(index);

                    table.Cell().Element(c => c.TableDataCell(background, emphasize: true))
                        .Text(CorporateReportFormatters.NullSafe(item.Clave));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Nombre));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Direccion));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Telefono));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(item.ActivoLabel);

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(item.EmpleadosActivos.ToString(CultureInfo.InvariantCulture));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(item.Id.ToString(CultureInfo.InvariantCulture));
                }
            });
        });
    }

    private static string BuildFileName(SucursalReporteQueryDto query, string extension)
    {
        return CorporateReportFormatters.BuildTimestampedFileName(
            "sucursales",
            extension,
            query.Activo.HasValue ? (query.Activo.Value ? "activas" : "inactivas") : null,
            string.IsNullOrWhiteSpace(query.Q) ? null : "busqueda");
    }

    private sealed class SucursalReportFilterLabels
    {
        public string Activo { get; set; } = "(todos)";
        public string Search { get; set; } = "(vacío)";
    }

    private sealed class SucursalExportRow
    {
        public int Id { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
        public bool Activo { get; set; }
        public string ActivoLabel { get; set; } = "No";
        public int EmpleadosActivos { get; set; }
    }
}