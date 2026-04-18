using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Empleados;
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

public sealed class EmpleadosReportService : IEmpleadosReportService
{
    private readonly RhDbContext _context;

    public EmpleadosReportService(RhDbContext context)
    {
        _context = context;
    }

    public async Task<ReportFileDto> BuildXlsxAsync(
        EmpleadosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetEmpleadosExportRowsAsync(query, cancellationToken);
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
        EmpleadosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetEmpleadosExportRowsAsync(query, cancellationToken);
        var filterLabels = await ResolveFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        var total = rows.Count;
        var activos = rows.Count(x => string.Equals(x.Activo, "Sí", StringComparison.OrdinalIgnoreCase));
        var inactivos = rows.Count - activos;
        var sucursales = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.Sucursal, "(sin sucursal)"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var departamentos = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.Departamento, "(sin departamento)"))
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
                        "Reporte de empleados",
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
                            ("Total de empleados", total.ToString(CultureInfo.InvariantCulture), "Registros visibles en el reporte", CorporateReportPalette.KpiPrimary),
                            ("Activos", activos.ToString(CultureInfo.InvariantCulture), "Personal vigente", CorporateReportPalette.KpiSuccess),
                            ("Inactivos", inactivos.ToString(CultureInfo.InvariantCulture), "Personal no activo", CorporateReportPalette.KpiWarning),
                            ("Sucursales", sucursales.ToString(CultureInfo.InvariantCulture), "Cobertura visible", CorporateReportPalette.KpiPurple),
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

    private IQueryable<Empleado> BuildBaseQuery(EmpleadosReporteQueryDto query)
    {
        var empleadosQuery = _context.Empleados
            .AsNoTracking()
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Include(x => x.Sucursal)
            .AsQueryable();

        if (query.SucursalId.HasValue)
            empleadosQuery = empleadosQuery.Where(x => x.SucursalId == query.SucursalId.Value);

        if (query.DepartamentoId.HasValue)
            empleadosQuery = empleadosQuery.Where(x => x.DepartamentoId == query.DepartamentoId.Value);

        if (query.PuestoId.HasValue)
            empleadosQuery = empleadosQuery.Where(x => x.PuestoId == query.PuestoId.Value);

        if (query.Activo.HasValue)
            empleadosQuery = empleadosQuery.Where(x => x.Activo == query.Activo.Value);

        if (!string.IsNullOrWhiteSpace(query.EstatusLaboral))
            empleadosQuery = empleadosQuery.Where(x => x.EstatusLaboralActual.ToString() == query.EstatusLaboral);

        if (query.FechaIngresoDesde.HasValue)
            empleadosQuery = empleadosQuery.Where(x => x.FechaIngreso >= query.FechaIngresoDesde.Value);

        if (query.FechaIngresoHasta.HasValue)
            empleadosQuery = empleadosQuery.Where(x => x.FechaIngreso <= query.FechaIngresoHasta.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();

            empleadosQuery = empleadosQuery.Where(x =>
                EF.Functions.ILike(x.NumEmpleado, $"%{term}%") ||
                EF.Functions.ILike(x.Nombres, $"%{term}%") ||
                EF.Functions.ILike(x.ApellidoPaterno, $"%{term}%") ||
                (x.ApellidoMaterno != null && EF.Functions.ILike(x.ApellidoMaterno, $"%{term}%")) ||
                (x.Email != null && EF.Functions.ILike(x.Email, $"%{term}%")) ||
                (x.Telefono != null && EF.Functions.ILike(x.Telefono, $"%{term}%")) ||
                (x.Departamento != null && EF.Functions.ILike(x.Departamento.Nombre, $"%{term}%")) ||
                (x.Puesto != null && EF.Functions.ILike(x.Puesto.Nombre, $"%{term}%")) ||
                (x.Sucursal != null && EF.Functions.ILike(x.Sucursal.Nombre, $"%{term}%")) ||
                (x.Sucursal != null && EF.Functions.ILike(x.Sucursal.Clave, $"%{term}%"))
            );
        }

        return empleadosQuery;
    }

    private async Task<List<EmpleadoExportRowDto>> GetEmpleadosExportRowsAsync(
        EmpleadosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rawRows = await BuildBaseQuery(query)
            .OrderBy(x => x.NumEmpleado)
            .ThenBy(x => x.ApellidoPaterno)
            .ThenBy(x => x.Nombres)
            .Select(x => new
            {
                x.Id,
                x.NumEmpleado,
                x.Nombres,
                x.ApellidoPaterno,
                x.ApellidoMaterno,
                Sucursal = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Departamento = x.Departamento != null ? x.Departamento.Nombre : null,
                Puesto = x.Puesto != null ? x.Puesto.Nombre : null,
                x.FechaIngreso,
                x.Telefono,
                x.Email,
                EstatusLaboral = x.EstatusLaboralActual.ToString(),
                x.Activo
            })
            .ToListAsync(cancellationToken);

        return rawRows.Select(x => new EmpleadoExportRowDto
        {
            Id = x.Id,
            NumEmpleado = x.NumEmpleado,
            NombreCompleto = CorporateReportFormatters.CombineFullName(
                x.Nombres,
                x.ApellidoPaterno,
                x.ApellidoMaterno),
            Sucursal = x.Sucursal,
            Departamento = x.Departamento,
            Puesto = x.Puesto,
            FechaIngreso = x.FechaIngreso,
            Telefono = x.Telefono,
            Email = x.Email,
            EstatusLaboral = x.EstatusLaboral,
            Activo = x.Activo ? "Sí" : "No"
        }).ToList();
    }

    private async Task<EmpleadosReportFilterLabels> ResolveFilterLabelsAsync(
        EmpleadosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = new EmpleadosReportFilterLabels
        {
            Sucursal = "(todas)",
            Departamento = "(todos)",
            Puesto = "(todos)",
            Activo = query.Activo.HasValue
                ? CorporateReportFormatters.FormatBool(query.Activo.Value)
                : "(todos)",
            EstatusLaboral = string.IsNullOrWhiteSpace(query.EstatusLaboral)
                ? "(todos)"
                : CorporateReportFormatters.FormatLabel(query.EstatusLaboral),
            FechaIngresoDesde = CorporateReportFormatters.FormatDateNullable(query.FechaIngresoDesde, fallback: "(sin límite)"),
            FechaIngresoHasta = CorporateReportFormatters.FormatDateNullable(query.FechaIngresoHasta, fallback: "(sin límite)"),
            PeriodoIngreso = CorporateReportFormatters.FormatDateRange(query.FechaIngresoDesde, query.FechaIngresoHasta),
            Search = CorporateReportFormatters.FormatSearchTerm(query.Search)
        };

        if (query.SucursalId.HasValue)
        {
            result.Sucursal = await _context.Sucursales
                .AsNoTracking()
                .Where(x => x.Id == query.SucursalId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.SucursalId.Value}";
        }

        if (query.DepartamentoId.HasValue)
        {
            result.Departamento = await _context.Departamentos
                .AsNoTracking()
                .Where(x => x.Id == query.DepartamentoId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.DepartamentoId.Value}";
        }

        if (query.PuestoId.HasValue)
        {
            result.Puesto = await _context.Puestos
                .AsNoTracking()
                .Where(x => x.Id == query.PuestoId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{query.PuestoId.Value}";
        }

        return result;
    }

    private static IReadOnlyList<(string Label, string Value)> BuildFilterItems(EmpleadosReportFilterLabels labels)
    {
        return
        [
            ("Sucursal", labels.Sucursal),
            ("Departamento", labels.Departamento),
            ("Puesto", labels.Puesto),
            ("Activo", labels.Activo),
            ("Estatus laboral", labels.EstatusLaboral),
            ("Periodo ingreso", labels.PeriodoIngreso),
            ("Búsqueda", labels.Search)
        ];
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        IReadOnlyList<EmpleadoExportRowDto> rows,
        EmpleadosReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de empleados",
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
        var activos = rows.Count(x => string.Equals(x.Activo, "Sí", StringComparison.OrdinalIgnoreCase));
        var inactivos = total - activos;
        var sucursales = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.Sucursal, "(sin sucursal)"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var departamentos = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.Departamento, "(sin departamento)"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        nextRow = CorporateExcelStyles.WriteKpiCards(
            ws,
            nextRow,
            new (string Title, string Value, string AccentColor)[]
            {
                ("Total de empleados", total.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPrimary),
                ("Activos", activos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiSuccess),
                ("Inactivos", inactivos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiWarning),
                ("Sucursales", sucursales.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPurple),
                ("Departamentos", departamentos.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiTeal)
            });

        nextRow += 2;

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 1,
            sectionTitle: "Por estatus laboral",
            header1: "Estatus",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.FormatLabel(x.EstatusLaboral))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 4,
            sectionTitle: "Por sucursal",
            header1: "Sucursal",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.NullSafe(x.Sucursal, "(sin sucursal)"))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 7,
            sectionTitle: "Por departamento",
            header1: "Departamento",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.NullSafe(x.Departamento, "(sin departamento)"))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .ToList());

        CorporateExcelStyles.SetColumnWidths(ws, 24, 18, 4, 24, 18, 4, 24, 18, 4, 16);
    }

    private static void BuildDetalleSheet(
        XLWorkbook workbook,
        IReadOnlyList<EmpleadoExportRowDto> rows,
        EmpleadosReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Empleados");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de empleados",
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

        ws.Cell(headerRow, 1).Value = "Núm. Emp.";
        ws.Cell(headerRow, 2).Value = "Nombre";
        ws.Cell(headerRow, 3).Value = "Sucursal";
        ws.Cell(headerRow, 4).Value = "Departamento";
        ws.Cell(headerRow, 5).Value = "Puesto";
        ws.Cell(headerRow, 6).Value = "Ingreso";
        ws.Cell(headerRow, 7).Value = "Teléfono";
        ws.Cell(headerRow, 8).Value = "Correo";
        ws.Cell(headerRow, 9).Value = "Estatus laboral";
        ws.Cell(headerRow, 10).Value = "Activo";

        CorporateExcelStyles.ApplyTableHeader(ws.Range(headerRow, 1, headerRow, 10));

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = CorporateReportFormatters.ExcelText(item.NumEmpleado);
            ws.Cell(rowIndex, 2).Value = CorporateReportFormatters.ExcelText(item.NombreCompleto);
            ws.Cell(rowIndex, 3).Value = CorporateReportFormatters.ExcelText(item.Sucursal, "(sin sucursal)");
            ws.Cell(rowIndex, 4).Value = CorporateReportFormatters.ExcelText(item.Departamento, "(sin departamento)");
            ws.Cell(rowIndex, 5).Value = CorporateReportFormatters.ExcelText(item.Puesto, "(sin puesto)");
            ws.Cell(rowIndex, 6).Value = CorporateReportFormatters.ExcelDate(item.FechaIngreso);
            ws.Cell(rowIndex, 7).Value = CorporateReportFormatters.ExcelText(item.Telefono);
            ws.Cell(rowIndex, 8).Value = CorporateReportFormatters.ExcelText(item.Email);
            ws.Cell(rowIndex, 9).Value = CorporateReportFormatters.FormatLabel(item.EstatusLaboral);
            ws.Cell(rowIndex, 10).Value = item.Activo;

            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;

        CorporateExcelStyles.ApplyDataGrid(ws, headerRow + 1, lastDataRow, 1, 10);
        CorporateExcelStyles.FinalizeTable(ws, headerRow, 1, 10, lastDataRow);

        CorporateExcelStyles.SetColumnWidths(ws,
            14, // NumEmp
            30, // Nombre
            22, // Sucursal
            22, // Departamento
            22, // Puesto
            14, // Ingreso
            16, // Teléfono
            28, // Correo
            18, // Estatus
            12  // Activo
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
        IReadOnlyList<EmpleadoExportRowDto> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Distribución", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(12);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);

                    left.Item().Text("Por estatus laboral")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.FormatLabel(x.EstatusLaboral))
                        .OrderBy(x => x.Key)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        left.Item().Text("Sin empleados para resumir.")
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

                    right.Item().Text("Por sucursal")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.NullSafe(x.Sucursal, "(sin sucursal)"))
                        .OrderBy(x => x.Key)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        right.Item().Text("Sin empleados para resumir.")
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
        IReadOnlyList<EmpleadoExportRowDto> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Detalle de empleados", body =>
        {
            if (rows.Count == 0)
            {
                body.Item().Element(c =>
                    CorporatePdfBlocks.ComposeEmptyState(c, "No se encontraron empleados con los filtros aplicados."));
                return;
            }

            body.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(76);    // NumEmp
                    columns.RelativeColumn(3.0f);  // Nombre
                    columns.RelativeColumn(1.9f);  // Sucursal
                    columns.RelativeColumn(1.8f);  // Departamento
                    columns.RelativeColumn(1.7f);  // Puesto
                    columns.ConstantColumn(66);    // Ingreso
                    columns.ConstantColumn(82);    // Teléfono
                    columns.RelativeColumn(2.4f);  // Correo
                    columns.ConstantColumn(76);    // Estatus
                    columns.ConstantColumn(46);    // Activo
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Núm. Emp.");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Nombre");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Sucursal");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Departamento");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Puesto");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Ingreso");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Teléfono");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Correo");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Estatus");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Activo");
                });

                for (var index = 0; index < rows.Count; index++)
                {
                    var item = rows[index];
                    var background = CorporatePdfStyles.ZebraRow(index);

                    table.Cell().Element(c => c.TableDataCell(background, emphasize: true))
                        .Text(CorporateReportFormatters.NullSafe(item.NumEmpleado));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.NombreCompleto));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Sucursal, "(sin sucursal)"));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Departamento, "(sin departamento)"));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Puesto, "(sin puesto)"));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDate(item.FechaIngreso));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Telefono));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.Email));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatLabel(item.EstatusLaboral));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(item.Activo);
                }
            });
        });
    }

    private static string BuildFileName(EmpleadosReporteQueryDto query, string extension)
    {
        return CorporateReportFormatters.BuildTimestampedFileName(
            "empleados",
            extension,
            query.SucursalId.HasValue ? $"sucursal_{query.SucursalId.Value}" : null,
            query.DepartamentoId.HasValue ? $"departamento_{query.DepartamentoId.Value}" : null,
            query.PuestoId.HasValue ? $"puesto_{query.PuestoId.Value}" : null,
            query.Activo.HasValue ? (query.Activo.Value ? "activos" : "inactivos") : null);
    }

    private sealed class EmpleadosReportFilterLabels
    {
        public string Sucursal { get; set; } = "(todas)";
        public string Departamento { get; set; } = "(todos)";
        public string Puesto { get; set; } = "(todos)";
        public string Activo { get; set; } = "(todos)";
        public string EstatusLaboral { get; set; } = "(todos)";
        public string FechaIngresoDesde { get; set; } = "(sin límite)";
        public string FechaIngresoHasta { get; set; } = "(sin límite)";
        public string PeriodoIngreso { get; set; } = "(sin límite)";
        public string Search { get; set; } = "(vacío)";
    }
}