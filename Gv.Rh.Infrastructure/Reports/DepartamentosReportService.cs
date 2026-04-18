using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Departamentos;
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

public sealed class DepartamentosReportService : IDepartamentosReportService
{
    private readonly RhDbContext _context;

    public DepartamentosReportService(RhDbContext context)
    {
        _context = context;
    }

    public async Task<ReportFileDto> BuildXlsxAsync(
        DepartamentoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetDepartamentosExportRowsAsync(query, cancellationToken);
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
        DepartamentoReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetDepartamentosExportRowsAsync(query, cancellationToken);
        var filterLabels = ResolveFilterLabels(query);
        var generatedAtUtc = DateTime.UtcNow;

        var total = rows.Count;
        var activos = rows.Count(x => x.Activo);
        var inactivos = total - activos;

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
                        "Reporte de departamentos",
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
                            ("Total", total.ToString(CultureInfo.InvariantCulture), "Departamentos visibles", CorporateReportPalette.KpiPrimary),
                            ("Activos", activos.ToString(CultureInfo.InvariantCulture), "Disponibles para operación", CorporateReportPalette.KpiSuccess),
                            ("Inactivos", inactivos.ToString(CultureInfo.InvariantCulture), "Fuera de operación", CorporateReportPalette.KpiWarning)));

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

    private IQueryable<Departamento> BuildBaseQuery(DepartamentoReporteQueryDto query)
    {
        var departamentosQuery = _context.Departamentos
            .AsNoTracking()
            .AsQueryable();

        if (query.Activo.HasValue)
            departamentosQuery = departamentosQuery.Where(x => x.Activo == query.Activo.Value);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();

            departamentosQuery = departamentosQuery.Where(x =>
                EF.Functions.ILike(x.Clave, $"%{term}%") ||
                EF.Functions.ILike(x.Nombre, $"%{term}%"));
        }

        return departamentosQuery;
    }

    private async Task<List<DepartamentoExportRow>> GetDepartamentosExportRowsAsync(
        DepartamentoReporteQueryDto query,
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
                x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rawRows.Select(x => new DepartamentoExportRow
        {
            Id = x.Id,
            Clave = x.Clave,
            Nombre = x.Nombre,
            Activo = x.Activo,
            ActivoLabel = CorporateReportFormatters.FormatBool(x.Activo),
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        }).ToList();
    }

    private static DepartamentoReportFilterLabels ResolveFilterLabels(DepartamentoReporteQueryDto query)
    {
        return new DepartamentoReportFilterLabels
        {
            Activo = query.Activo.HasValue
                ? CorporateReportFormatters.FormatBool(query.Activo.Value)
                : "(todos)",
            Search = CorporateReportFormatters.FormatSearchTerm(query.Q)
        };
    }

    private static IReadOnlyList<(string Label, string Value)> BuildFilterItems(DepartamentoReportFilterLabels labels)
    {
        return
        [
            ("Activo", labels.Activo),
            ("Búsqueda", labels.Search)
        ];
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        IReadOnlyList<DepartamentoExportRow> rows,
        DepartamentoReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de departamentos",
            "Resumen ejecutivo",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 8,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildFilterItems(filterLabels));

        nextRow += 1;

        var total = rows.Count;
        var activos = rows.Count(x => x.Activo);
        var inactivos = total - activos;

        nextRow = CorporateExcelStyles.WriteKpiCards(
            ws,
            nextRow,
            new (string Title, string Value, string AccentColor)[]
            {
                ("Total", total.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPrimary),
                ("Activos", activos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiSuccess),
                ("Inactivos", inactivos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiWarning)
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
            sectionTitle: "Claves visibles",
            header1: "Clave",
            header2: "Nombre",
            rows: rows
                .Take(12)
                .Select(x => (x.Clave, x.Nombre))
                .ToList());

        CorporateExcelStyles.SetColumnWidths(ws, 22, 18, 4, 18, 30, 4, 16, 16);
    }

    private static void BuildDetalleSheet(
        XLWorkbook workbook,
        IReadOnlyList<DepartamentoExportRow> rows,
        DepartamentoReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Departamentos");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de departamentos",
            "Detalle operativo",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 6,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildFilterItems(filterLabels));

        nextRow += 2;

        var headerRow = nextRow;

        ws.Cell(headerRow, 1).Value = "Clave";
        ws.Cell(headerRow, 2).Value = "Nombre";
        ws.Cell(headerRow, 3).Value = "Activo";
        ws.Cell(headerRow, 4).Value = "Creado UTC";
        ws.Cell(headerRow, 5).Value = "Actualizado UTC";
        ws.Cell(headerRow, 6).Value = "Id";

        CorporateExcelStyles.ApplyTableHeader(ws.Range(headerRow, 1, headerRow, 6));

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = CorporateReportFormatters.ExcelText(item.Clave);
            ws.Cell(rowIndex, 2).Value = CorporateReportFormatters.ExcelText(item.Nombre);
            ws.Cell(rowIndex, 3).Value = item.ActivoLabel;
            ws.Cell(rowIndex, 4).Value = CorporateReportFormatters.ExcelDateTime(item.CreatedAtUtc);
            ws.Cell(rowIndex, 5).Value = CorporateReportFormatters.ExcelDateTime(item.UpdatedAtUtc);
            ws.Cell(rowIndex, 6).Value = item.Id;

            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;

        CorporateExcelStyles.ApplyDataGrid(ws, headerRow + 1, lastDataRow, 1, 6);
        CorporateExcelStyles.FinalizeTable(ws, headerRow, 1, 6, lastDataRow);

        CorporateExcelStyles.SetColumnWidths(ws,
            16, // Clave
            34, // Nombre
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

    private static void WriteSummaryTable(
        IXLWorksheet ws,
        int startRow,
        int startColumn,
        string sectionTitle,
        string header1,
        string header2,
        IReadOnlyList<(string Left, string Right)> rows)
    {
        CorporateExcelStyles.WriteSectionTitle(ws, startRow, startColumn, sectionTitle);

        ws.Cell(startRow + 1, startColumn).Value = header1;
        ws.Cell(startRow + 1, startColumn + 1).Value = header2;

        CorporateExcelStyles.ApplyTableHeader(ws.Range(startRow + 1, startColumn, startRow + 1, startColumn + 1));

        var rowIndex = startRow + 2;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, startColumn).Value = item.Left;
            ws.Cell(rowIndex, startColumn + 1).Value = item.Right;
            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;
        CorporateExcelStyles.ApplyDataGrid(ws, startRow + 2, lastDataRow, startColumn, startColumn + 1);
    }

    private static void ComposeDistributionSection(
        IContainer container,
        IReadOnlyList<DepartamentoExportRow> rows)
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

                    right.Item().Text("Claves visibles")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    if (rows.Count == 0)
                    {
                        right.Item().Text("Sin departamentos para resumir.")
                            .FontSize(8.5f)
                            .FontColor(CorporateReportPalette.Ink500);
                    }
                    else
                    {
                        foreach (var item in rows.Take(8))
                        {
                            right.Item().Text(text =>
                            {
                                text.Span($"{item.Clave}: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
                                text.Span(item.Nombre).FontColor(CorporateReportPalette.Ink900);
                            });
                        }
                    }
                });
            });
        });
    }

    private static void ComposeDetailSection(
        IContainer container,
        IReadOnlyList<DepartamentoExportRow> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Detalle de departamentos", body =>
        {
            if (rows.Count == 0)
            {
                body.Item().Element(c =>
                    CorporatePdfBlocks.ComposeEmptyState(c, "No se encontraron departamentos con los filtros aplicados."));
                return;
            }

            body.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(72);     // Clave
                    columns.RelativeColumn(2.8f);   // Nombre
                    columns.ConstantColumn(54);     // Activo
                    columns.ConstantColumn(88);     // Creado
                    columns.ConstantColumn(88);     // Actualizado
                    columns.ConstantColumn(34);     // Id
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Clave");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Nombre");
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

    private static string BuildFileName(DepartamentoReporteQueryDto query, string extension)
    {
        return CorporateReportFormatters.BuildTimestampedFileName(
            "departamentos",
            extension,
            query.Activo.HasValue ? (query.Activo.Value ? "activos" : "inactivos") : null,
            string.IsNullOrWhiteSpace(query.Q) ? null : "busqueda");
    }

    private sealed class DepartamentoReportFilterLabels
    {
        public string Activo { get; set; } = "(todos)";
        public string Search { get; set; } = "(vacío)";
    }

    private sealed class DepartamentoExportRow
    {
        public int Id { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public string ActivoLabel { get; set; } = "No";
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}