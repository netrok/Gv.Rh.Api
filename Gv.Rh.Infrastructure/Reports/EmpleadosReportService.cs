using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Empleados;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
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

        using var workbook = new XLWorkbook();

        BuildEmpleadosSheet(workbook, rows, filterLabels);
        BuildResumenSheet(workbook, rows, filterLabels);

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

        var pdfBytes = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#1F2937"));

                page.Header().Element(container => ComposeHeader(container, generatedAtUtc));

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(container => ComposeFilters(container, filterLabels));
                    column.Item().Element(container => ComposeSummary(container, rows));
                    column.Item().Element(container => ComposeDetailTable(container, rows));
                });

                page.Footer().Element(container => ComposeFooter(container, generatedAtUtc));
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
            NombreCompleto = string.Join(" ", new[]
            {
                x.Nombres,
                x.ApellidoPaterno,
                x.ApellidoMaterno
            }.Where(s => !string.IsNullOrWhiteSpace(s))),
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
            Activo = query.Activo.HasValue ? (query.Activo.Value ? "Sí" : "No") : "(todos)",
            EstatusLaboral = string.IsNullOrWhiteSpace(query.EstatusLaboral)
                ? "(todos)"
                : FormatLabel(query.EstatusLaboral),
            FechaIngresoDesde = query.FechaIngresoDesde?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "(sin límite)",
            FechaIngresoHasta = query.FechaIngresoHasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "(sin límite)",
            Search = string.IsNullOrWhiteSpace(query.Search) ? "(vacío)" : query.Search.Trim()
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

    private static void BuildEmpleadosSheet(
        XLWorkbook workbook,
        IReadOnlyList<EmpleadoExportRowDto> rows,
        EmpleadosReportFilterLabels filterLabels)
    {
        var ws = workbook.Worksheets.Add("Empleados");

        ws.Cell(1, 1).Value = "Reporte de empleados";
        ws.Cell(2, 1).Value = "Generado UTC";
        ws.Cell(2, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        ws.Range(1, 1, 1, 10).Merge().Style.Font.Bold = true;
        ws.Range(1, 1, 1, 10).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 10).Style.Font.FontColor = XLColor.FromHtml("#0F172A");

        WriteFilters(ws, filterLabels, 4);

        const int headerRow = 13;

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

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = item.NumEmpleado;
            ws.Cell(rowIndex, 2).Value = item.NombreCompleto;
            ws.Cell(rowIndex, 3).Value = item.Sucursal ?? string.Empty;
            ws.Cell(rowIndex, 4).Value = item.Departamento ?? string.Empty;
            ws.Cell(rowIndex, 5).Value = item.Puesto ?? string.Empty;
            ws.Cell(rowIndex, 6).Value = item.FechaIngreso.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            ws.Cell(rowIndex, 7).Value = item.Telefono ?? string.Empty;
            ws.Cell(rowIndex, 8).Value = item.Email ?? string.Empty;
            ws.Cell(rowIndex, 9).Value = FormatLabel(item.EstatusLaboral);
            ws.Cell(rowIndex, 10).Value = item.Activo;
            rowIndex++;
        }

        var headerRange = ws.Range(headerRow, 1, headerRow, 10);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var lastDataRow = Math.Max(headerRow + 1, rowIndex - 1);
        ws.Range(headerRow, 1, lastDataRow, 10).SetAutoFilter();
        ws.SheetView.FreezeRows(headerRow);

        ws.Column(1).Width = 14;
        ws.Column(2).Width = 30;
        ws.Column(3).Width = 22;
        ws.Column(4).Width = 22;
        ws.Column(5).Width = 22;
        ws.Column(6).Width = 14;
        ws.Column(7).Width = 16;
        ws.Column(8).Width = 28;
        ws.Column(9).Width = 18;
        ws.Column(10).Width = 12;
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        IReadOnlyList<EmpleadoExportRowDto> rows,
        EmpleadosReportFilterLabels filterLabels)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        ws.Cell(1, 1).Value = "Resumen de empleados";
        ws.Cell(2, 1).Value = "Generado UTC";
        ws.Cell(2, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        WriteFilters(ws, filterLabels, 4);

        ws.Cell(13, 1).Value = "Total";
        ws.Cell(13, 2).Value = rows.Count;

        ws.Cell(14, 1).Value = "Activos";
        ws.Cell(14, 2).Value = rows.Count(x => x.Activo == "Sí");

        ws.Cell(15, 1).Value = "Inactivos";
        ws.Cell(15, 2).Value = rows.Count(x => x.Activo == "No");

        ws.Cell(17, 1).Value = "Por estatus laboral";
        ws.Cell(18, 1).Value = "Estatus";
        ws.Cell(18, 2).Value = "Cantidad";

        var statusRow = 19;
        foreach (var group in rows.GroupBy(x => FormatLabel(x.EstatusLaboral)).OrderBy(x => x.Key))
        {
            ws.Cell(statusRow, 1).Value = group.Key;
            ws.Cell(statusRow, 2).Value = group.Count();
            statusRow++;
        }

        ws.Cell(17, 4).Value = "Por sucursal";
        ws.Cell(18, 4).Value = "Sucursal";
        ws.Cell(18, 5).Value = "Cantidad";

        var sucursalRow = 19;
        foreach (var group in rows.GroupBy(x => x.Sucursal ?? "(sin sucursal)").OrderBy(x => x.Key))
        {
            ws.Cell(sucursalRow, 4).Value = group.Key;
            ws.Cell(sucursalRow, 5).Value = group.Count();
            sucursalRow++;
        }

        ws.Range(18, 1, 18, 2).Style.Font.Bold = true;
        ws.Range(18, 4, 18, 5).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }

    private static void WriteFilters(
        IXLWorksheet ws,
        EmpleadosReportFilterLabels filterLabels,
        int startRow)
    {
        ws.Cell(startRow, 1).Value = "Sucursal";
        ws.Cell(startRow, 2).Value = filterLabels.Sucursal;

        ws.Cell(startRow + 1, 1).Value = "Departamento";
        ws.Cell(startRow + 1, 2).Value = filterLabels.Departamento;

        ws.Cell(startRow + 2, 1).Value = "Puesto";
        ws.Cell(startRow + 2, 2).Value = filterLabels.Puesto;

        ws.Cell(startRow + 3, 1).Value = "Activo";
        ws.Cell(startRow + 3, 2).Value = filterLabels.Activo;

        ws.Cell(startRow + 4, 1).Value = "Estatus laboral";
        ws.Cell(startRow + 4, 2).Value = filterLabels.EstatusLaboral;

        ws.Cell(startRow + 5, 1).Value = "Ingreso desde";
        ws.Cell(startRow + 5, 2).Value = filterLabels.FechaIngresoDesde;

        ws.Cell(startRow + 6, 1).Value = "Ingreso hasta";
        ws.Cell(startRow + 6, 2).Value = filterLabels.FechaIngresoHasta;

        ws.Cell(startRow + 7, 1).Value = "Búsqueda";
        ws.Cell(startRow + 7, 2).Value = filterLabels.Search;

        ws.Range(startRow, 1, startRow + 7, 1).Style.Font.Bold = true;
    }

    private static void ComposeHeader(IContainer container, DateTime generatedAtUtc)
    {
        container.PaddingBottom(6).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Spacing(2);

                    left.Item().Text("GV RH · Recursos Humanos")
                        .FontSize(10)
                        .FontColor("#475569")
                        .SemiBold();

                    left.Item().Text("Reporte de empleados")
                        .FontSize(20)
                        .FontColor("#0F172A")
                        .Bold();

                    left.Item().Text($"Generado UTC: {generatedAtUtc:yyyy-MM-dd HH:mm:ss}")
                        .FontSize(9)
                        .FontColor("#64748B");
                });

                row.ConstantItem(180).AlignRight().AlignMiddle().Text("Reporte corporativo")
                    .FontSize(9)
                    .FontColor("#1D4ED8")
                    .SemiBold();
            });

            column.Item().PaddingTop(6).LineHorizontal(1).LineColor("#D7DFEA");
        });
    }

    private static void ComposeFilters(
        IContainer container,
        EmpleadosReportFilterLabels filterLabels)
    {
        container
            .Background("#F8FAFC")
            .Border(1)
            .BorderColor("#D7DFEA")
            .CornerRadius(8)
            .Padding(10)
            .Column(column =>
            {
                column.Spacing(8);

                column.Item().Text("Filtros aplicados")
                    .FontSize(11)
                    .Bold()
                    .FontColor("#0F172A");

                column.Item().Row(row =>
                {
                    row.Spacing(18);

                    row.RelativeItem().Column(left =>
                    {
                        left.Spacing(4);
                        AddFilterText(left, "Sucursal", filterLabels.Sucursal);
                        AddFilterText(left, "Departamento", filterLabels.Departamento);
                        AddFilterText(left, "Puesto", filterLabels.Puesto);
                        AddFilterText(left, "Activo", filterLabels.Activo);
                    });

                    row.RelativeItem().Column(right =>
                    {
                        right.Spacing(4);
                        AddFilterText(right, "Estatus laboral", filterLabels.EstatusLaboral);
                        AddFilterText(right, "Ingreso desde", filterLabels.FechaIngresoDesde);
                        AddFilterText(right, "Ingreso hasta", filterLabels.FechaIngresoHasta);
                        AddFilterText(right, "Búsqueda", filterLabels.Search);
                    });
                });
            });
    }

    private static void AddFilterText(ColumnDescriptor column, string label, string value)
    {
        column.Item().Text(text =>
        {
            text.Span($"{label}: ").SemiBold().FontColor("#334155");
            text.Span(value).FontColor("#0F172A");
        });
    }

    private static void ComposeSummary(IContainer container, IReadOnlyList<EmpleadoExportRowDto> rows)
    {
        var activos = rows.Count(x => x.Activo == "Sí");
        var inactivos = rows.Count(x => x.Activo == "No");

        container.Row(row =>
        {
            row.Spacing(10);

            SummaryCard(
                row.RelativeItem(),
                "Total de empleados",
                rows.Count.ToString(CultureInfo.InvariantCulture),
                "Registros visibles en el reporte",
                "#1D4ED8");

            SummaryCard(
                row.RelativeItem(),
                "Activos",
                activos.ToString(CultureInfo.InvariantCulture),
                "Personal vigente",
                "#15803D");

            SummaryCard(
                row.RelativeItem(),
                "Inactivos",
                inactivos.ToString(CultureInfo.InvariantCulture),
                "Personal no activo",
                "#B45309");
        });
    }

    private static void SummaryCard(
        IContainer container,
        string title,
        string value,
        string subtitle,
        string accentColor)
    {
        container
            .Background("#FFFFFF")
            .Border(1)
            .BorderColor("#D7DFEA")
            .CornerRadius(8)
            .Padding(12)
            .Column(column =>
            {
                column.Spacing(4);

                column.Item().LineHorizontal(3).LineColor(accentColor);

                column.Item().PaddingTop(4).Text(title)
                    .FontSize(10)
                    .SemiBold()
                    .FontColor("#475569");

                column.Item().Text(value)
                    .FontSize(19)
                    .Bold()
                    .FontColor(accentColor);

                column.Item().Text(subtitle)
                    .FontSize(8.5f)
                    .FontColor("#64748B");
            });
    }

    private static void ComposeDetailTable(IContainer container, IReadOnlyList<EmpleadoExportRowDto> rows)
    {
        container.Column(column =>
        {
            column.Spacing(6);

            column.Item().Text("Detalle de empleados")
                .FontSize(11)
                .Bold()
                .FontColor("#0F172A");

            column.Item().Table(table =>
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
                    columns.RelativeColumn(2.5f);  // Correo
                    columns.ConstantColumn(74);    // Estatus
                    columns.ConstantColumn(46);    // Activo
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Núm. Emp.").SemiBold();
                    header.Cell().Element(HeaderCellStyle).Text("Nombre").SemiBold();
                    header.Cell().Element(HeaderCellStyle).Text("Sucursal").SemiBold();
                    header.Cell().Element(HeaderCellStyle).Text("Departamento").SemiBold();
                    header.Cell().Element(HeaderCellStyle).Text("Puesto").SemiBold();
                    header.Cell().Element(HeaderCellStyle).Text("Ingreso").SemiBold();
                    header.Cell().Element(HeaderCellStyle).Text("Teléfono").SemiBold();
                    header.Cell().Element(HeaderCellStyle).Text("Correo").SemiBold();
                    header.Cell().Element(HeaderCellStyle).Text("Estatus").SemiBold();
                    header.Cell().Element(HeaderCellStyle).Text("Activo").SemiBold();
                });

                if (rows.Count == 0)
                {
                    table.Cell()
                        .ColumnSpan(10)
                        .Element(EmptyCellStyle)
                        .Text("No se encontraron empleados con los filtros aplicados.");
                    return;
                }

                var index = 0;
                foreach (var item in rows)
                {
                    var background = index % 2 == 0 ? "#FFFFFF" : "#FAFCFF";

                    table.Cell().Element(c => DataCellStyle(c, background, true)).Text(item.NumEmpleado);
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(item.NombreCompleto);
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(item.Sucursal ?? "—");
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(item.Departamento ?? "—");
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(item.Puesto ?? "—");
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(item.FechaIngreso.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(item.Telefono ?? "—");
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(item.Email ?? "—");
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(FormatLabel(item.EstatusLaboral));
                    table.Cell().Element(c => DataCellStyle(c, background)).Text(item.Activo);

                    index++;
                }
            });
        });
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .Background("#214FC6")
            .Border(1)
            .BorderColor("#214FC6")
            .PaddingVertical(7)
            .PaddingHorizontal(6)
            .AlignCenter()
            .AlignMiddle()
            .DefaultTextStyle(x => x.FontColor("#FFFFFF").FontSize(8.1f).SemiBold());
    }

    private static IContainer DataCellStyle(
        IContainer container,
        string background,
        bool emphasis = false)
    {
        return container
            .Background(background)
            .BorderBottom(1)
            .BorderLeft(1)
            .BorderRight(1)
            .BorderColor("#DCE4F0")
            .PaddingVertical(6)
            .PaddingHorizontal(6)
            .DefaultTextStyle(x =>
            {
                var style = x.FontSize(8.6f).FontColor("#1F2937");
                return emphasis ? style.SemiBold() : style;
            });
    }

    private static IContainer EmptyCellStyle(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor("#D7DFEA")
            .Padding(10)
            .AlignCenter()
            .AlignMiddle()
            .DefaultTextStyle(x => x.FontSize(9).FontColor("#64748B"));
    }

    private static void ComposeFooter(IContainer container, DateTime generatedAtUtc)
    {
        container.PaddingTop(6).Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor("#D7DFEA");

            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"Emitido: {generatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC")
                    .FontSize(8)
                    .FontColor("#64748B");

                row.ConstantItem(90).AlignRight().Text(text =>
                {
                    text.Span("Página ").FontSize(8).FontColor("#64748B");
                    text.CurrentPageNumber().FontSize(8).SemiBold();
                    text.Span(" de ").FontSize(8).FontColor("#64748B");
                    text.TotalPages().FontSize(8).SemiBold();
                });
            });
        });
    }

    private static string FormatLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";

        return string.Join(
            " ",
            value.Trim()
                .Replace("_", " ")
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static string BuildFileName(EmpleadosReporteQueryDto query, string extension)
    {
        var parts = new List<string>
        {
            "empleados",
            DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
        };

        if (query.SucursalId.HasValue)
            parts.Add($"sucursal-{query.SucursalId.Value}");

        if (query.DepartamentoId.HasValue)
            parts.Add($"depto-{query.DepartamentoId.Value}");

        if (query.PuestoId.HasValue)
            parts.Add($"puesto-{query.PuestoId.Value}");

        if (query.Activo.HasValue)
            parts.Add(query.Activo.Value ? "activos" : "inactivos");

        return string.Join("_", parts) + "." + extension;
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
        public string Search { get; set; } = "(vacío)";
    }
}