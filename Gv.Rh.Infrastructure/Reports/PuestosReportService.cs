using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Puestos;
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

public sealed class PuestosReportService : IPuestosReportService
{
    private readonly RhDbContext _context;

    public PuestosReportService(RhDbContext context)
    {
        _context = context;
    }

    public async Task<ReportFileDto> BuildXlsxAsync(
        PuestoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetPuestosExportRowsAsync(query, cancellationToken);
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
        PuestoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetPuestosExportRowsAsync(query, cancellationToken);
        var filterLabels = await ResolveFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        var total = rows.Count;
        var activos = rows.Count(x => x.Activo);
        var inactivos = total - activos;
        var departamentos = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.DepartamentoNombre, "(sin departamento)"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
                        "Reporte de puestos",
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
                            ("Total", total.ToString(CultureInfo.InvariantCulture), "Puestos visibles", CorporateReportPalette.KpiPrimary),
                            ("Activos", activos.ToString(CultureInfo.InvariantCulture), "Disponibles para operación", CorporateReportPalette.KpiSuccess),
                            ("Inactivos", inactivos.ToString(CultureInfo.InvariantCulture), "Fuera de operación", CorporateReportPalette.KpiWarning),
                            ("Departamentos", departamentos.ToString(CultureInfo.InvariantCulture), "Áreas visibles", CorporateReportPalette.KpiTeal)));

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

    private IQueryable<Puesto> BuildBaseQuery(PuestoReporteQueryDto query)
    {
        var puestosQuery = _context.Puestos
            .AsNoTracking()
            .Include(x => x.Departamento)
            .AsQueryable();

        if (query.Activo.HasValue)
            puestosQuery = puestosQuery.Where(x => x.Activo == query.Activo.Value);

        if (query.DepartamentoId.HasValue)
            puestosQuery = puestosQuery.Where(x => x.DepartamentoId == query.DepartamentoId.Value);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();

            puestosQuery = puestosQuery.Where(x =>
                EF.Functions.ILike(x.Clave, $"%{term}%") ||
                EF.Functions.ILike(x.Nombre, $"%{term}%") ||
                (x.Departamento != null && EF.Functions.ILike(x.Departamento.Nombre, $"%{term}%")));
        }

        return puestosQuery;
    }

    private async Task<List<PuestoExportRow>> GetPuestosExportRowsAsync(
        PuestoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rawRows = await BuildBaseQuery(query)
            .OrderBy(x => x.Nombre)
            .ThenBy(x => x.Clave)
            .Select(x => new
            {
                x.Id,
                x.Clave,
                x.Nombre,
                x.Activo,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                DepartamentoNombre = x.Departamento != null ? x.Departamento.Nombre : null
            })
            .ToListAsync(cancellationToken);

        return rawRows.Select(x => new PuestoExportRow
        {
            Id = x.Id,
            Clave = x.Clave,
            Nombre = x.Nombre,
            DepartamentoNombre = x.DepartamentoNombre,
            Activo = x.Activo,
            ActivoLabel = CorporateReportFormatters.FormatBool(x.Activo),
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        }).ToList();
    }

    private async Task<PuestoReportFilterLabels> ResolveFilterLabelsAsync(
        PuestoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var labels = new PuestoReportFilterLabels
        {
            Activo = query.Activo.HasValue
                ? CorporateReportFormatters.FormatBool(query.Activo.Value)
                : "(todos)",
            Departamento = "(todos)",
            Search = CorporateReportFormatters.FormatSearchTerm(query.Q)
        };

        if (query.DepartamentoId.HasValue)
        {
            labels.Departamento = await _context.Departamentos
                .AsNoTracking()
                .Where(x => x.Id == query.DepartamentoId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.DepartamentoId.Value}";
        }

        return labels;
    }

    private static IReadOnlyList<(string Label, string Value)> BuildFilterItems(PuestoReportFilterLabels labels)
    {
        return
        [
            ("Departamento", labels.Departamento),
            ("Activo", labels.Activo),
            ("Búsqueda", labels.Search)
        ];
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        IReadOnlyList<PuestoExportRow> rows,
        PuestoReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de puestos",
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
        var activos = rows.Count(x => x.Activo);
        var inactivos = total - activos;
        var departamentos = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.DepartamentoNombre, "(sin departamento)"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        nextRow = CorporateExcelStyles.WriteKpiCards(
            ws,
            nextRow,
            new (string Title, string Value, string AccentColor)[]
            {
                ("Total", total.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPrimary),
                ("Activos", activos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiSuccess),
                ("Inactivos", inactivos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiWarning),
                ("Departamentos", departamentos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiTeal)
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
                ("Activos", activos),
                ("Inactivos", inactivos)
            ]);

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 4,
            sectionTitle: "Por departamento",
            header1: "Departamento",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.NullSafe(x.DepartamentoNombre, "(sin departamento)"))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .Take(12)
                .ToList());

        CorporateExcelStyles.SetColumnWidths(ws, 22, 18, 4, 24, 18, 4, 16, 16, 16);
    }

    private static void BuildDetalleSheet(
        XLWorkbook workbook,
        IReadOnlyList<PuestoExportRow> rows,
        PuestoReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Puestos");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de puestos",
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
        ws.Cell(headerRow, 3).Value = "Departamento";
        ws.Cell(headerRow, 4).Value = "Activo";
        ws.Cell(headerRow, 5).Value = "Creado UTC";
        ws.Cell(headerRow, 6).Value = "Actualizado UTC";
        ws.Cell(headerRow, 7).Value = "Id";

        CorporateExcelStyles.ApplyTableHeader(ws.Range(headerRow, 1, headerRow, 7));

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = CorporateReportFormatters.ExcelText(item.Clave);
            ws.Cell(rowIndex, 2).Value = CorporateReportFormatters.ExcelText(item.Nombre);
            ws.Cell(rowIndex, 3).Value = CorporateReportFormatters.ExcelText(item.DepartamentoNombre, "(sin departamento)");
            ws.Cell(rowIndex, 4).Value = item.ActivoLabel;
            ws.Cell(rowIndex, 5).Value = CorporateReportFormatters.ExcelDateTime(item.CreatedAtUtc);
            ws.Cell(rowIndex, 6).Value = CorporateReportFormatters.ExcelDateTime(item.UpdatedAtUtc);
            ws.Cell(rowIndex, 7).Value = item.Id;

            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;

        CorporateExcelStyles.ApplyDataGrid(ws, headerRow + 1, lastDataRow, 1, 7);
        CorporateExcelStyles.FinalizeTable(ws, headerRow, 1, 7, lastDataRow);

        CorporateExcelStyles.SetColumnWidths(ws,
            16, // Clave
            28, // Nombre
            24, // Departamento
            12, // Activo
            22, // Creado UTC
            22, // Actualizado UTC
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
        IReadOnlyList<PuestoExportRow> rows)
    {
        var activos = rows.Count(x => x.Activo);
        var inactivos = rows.Count - activos;

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
                        text.Span("Activos: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                        text.Span(activos.ToString(CultureInfo.InvariantCulture)).FontColor(CorporateReportPalette.Ink900);
                    });

                    left.Item().Text(text =>
                    {
                        text.Span("Inactivos: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                        text.Span(inactivos.ToString(CultureInfo.InvariantCulture)).FontColor(CorporateReportPalette.Ink900);
                    });
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);

                    right.Item().Text("Por departamento")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.NullSafe(x.DepartamentoNombre, "(sin departamento)"))
                        .OrderBy(x => x.Key)
                        .Take(8)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        right.Item().Text("Sin puestos para resumir.")
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
        IReadOnlyList<PuestoExportRow> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Detalle de puestos", body =>
        {
            if (rows.Count == 0)
            {
                body.Item().Element(c =>
                    CorporatePdfBlocks.ComposeEmptyState(c, "No se encontraron puestos con los filtros aplicados."));
                return;
            }

            body.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(72);     // Clave
                    columns.RelativeColumn(2.5f);   // Nombre
                    columns.RelativeColumn(2.1f);   // Departamento
                    columns.ConstantColumn(54);     // Activo
                    columns.ConstantColumn(88);     // Creado
                    columns.ConstantColumn(88);     // Actualizado
                    columns.ConstantColumn(34);     // Id
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Clave");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Nombre");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Departamento");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Activo");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Creado UTC");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Actualizado UTC");
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
                        .Text(CorporateReportFormatters.NullSafe(item.DepartamentoNombre, "(sin departamento)"));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(item.ActivoLabel);

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDateTime(item.CreatedAtUtc));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDateTime(item.UpdatedAtUtc));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(item.Id.ToString(CultureInfo.InvariantCulture));
                }
            });
        });
    }

    private static string BuildFileName(PuestoReporteQueryDto query, string extension)
    {
        return CorporateReportFormatters.BuildTimestampedFileName(
            "puestos",
            extension,
            query.DepartamentoId.HasValue ? $"departamento_{query.DepartamentoId.Value}" : null,
            query.Activo.HasValue ? (query.Activo.Value ? "activos" : "inactivos") : null,
            string.IsNullOrWhiteSpace(query.Q) ? null : "busqueda");
    }

    private sealed class PuestoReportFilterLabels
    {
        public string Departamento { get; set; } = "(todos)";
        public string Activo { get; set; } = "(todos)";
        public string Search { get; set; } = "(vacío)";
    }

    private sealed class PuestoExportRow
    {
        public int Id { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? DepartamentoNombre { get; set; }
        public bool Activo { get; set; }
        public string ActivoLabel { get; set; } = "No";
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}