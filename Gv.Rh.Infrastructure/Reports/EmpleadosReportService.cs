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

        using var workbook = new XLWorkbook();

        BuildEmpleadosSheet(workbook, rows, query);
        BuildResumenSheet(workbook, rows, query);

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

        var pdfBytes = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(column =>
                {
                    column.Spacing(4);

                    column.Item()
                        .Text("Reporte de empleados")
                        .SemiBold()
                        .FontSize(16);

                    column.Item()
                        .Text($"Generado UTC: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(container => ComposeFilters(container, query));
                    column.Item().Element(container => ComposeSummary(container, rows));
                    column.Item().Element(container => ComposeDetailTable(container, rows));
                });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Página ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
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

    private static void BuildEmpleadosSheet(
        XLWorkbook workbook,
        IReadOnlyList<EmpleadoExportRowDto> rows,
        EmpleadosReporteQueryDto query)
    {
        var ws = workbook.Worksheets.Add("Empleados");

        ws.Cell(1, 1).Value = "Reporte de empleados";
        ws.Cell(2, 1).Value = "Generado UTC";
        ws.Cell(2, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        WriteFilters(ws, query, 4);

        const int headerRow = 13;

        ws.Cell(headerRow, 1).Value = "ID";
        ws.Cell(headerRow, 2).Value = "NumEmpleado";
        ws.Cell(headerRow, 3).Value = "Nombre";
        ws.Cell(headerRow, 4).Value = "Sucursal";
        ws.Cell(headerRow, 5).Value = "Departamento";
        ws.Cell(headerRow, 6).Value = "Puesto";
        ws.Cell(headerRow, 7).Value = "FechaIngreso";
        ws.Cell(headerRow, 8).Value = "Telefono";
        ws.Cell(headerRow, 9).Value = "Email";
        ws.Cell(headerRow, 10).Value = "EstatusLaboral";
        ws.Cell(headerRow, 11).Value = "Activo";

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = item.Id;
            ws.Cell(rowIndex, 2).Value = item.NumEmpleado;
            ws.Cell(rowIndex, 3).Value = item.NombreCompleto;
            ws.Cell(rowIndex, 4).Value = item.Sucursal ?? string.Empty;
            ws.Cell(rowIndex, 5).Value = item.Departamento ?? string.Empty;
            ws.Cell(rowIndex, 6).Value = item.Puesto ?? string.Empty;
            ws.Cell(rowIndex, 7).Value = item.FechaIngreso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(rowIndex, 8).Value = item.Telefono ?? string.Empty;
            ws.Cell(rowIndex, 9).Value = item.Email ?? string.Empty;
            ws.Cell(rowIndex, 10).Value = item.EstatusLaboral;
            ws.Cell(rowIndex, 11).Value = item.Activo;
            rowIndex++;
        }

        var headerRange = ws.Range(headerRow, 1, headerRow, 11);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        var lastDataRow = Math.Max(headerRow + 1, rowIndex - 1);
        ws.Range(headerRow, 1, lastDataRow, 11).SetAutoFilter();

        ws.SheetView.FreezeRows(headerRow);
        ws.Columns().AdjustToContents();
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        IReadOnlyList<EmpleadoExportRowDto> rows,
        EmpleadosReporteQueryDto query)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        ws.Cell(1, 1).Value = "Resumen de empleados";
        ws.Cell(2, 1).Value = "Generado UTC";
        ws.Cell(2, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        WriteFilters(ws, query, 4);

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
        foreach (var group in rows.GroupBy(x => x.EstatusLaboral).OrderBy(x => x.Key))
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

    private static void WriteFilters(IXLWorksheet ws, EmpleadosReporteQueryDto query, int startRow)
    {
        ws.Cell(startRow, 1).Value = "SucursalId";
        ws.Cell(startRow, 2).Value = query.SucursalId?.ToString(CultureInfo.InvariantCulture) ?? "(todas)";

        ws.Cell(startRow + 1, 1).Value = "DepartamentoId";
        ws.Cell(startRow + 1, 2).Value = query.DepartamentoId?.ToString(CultureInfo.InvariantCulture) ?? "(todos)";

        ws.Cell(startRow + 2, 1).Value = "PuestoId";
        ws.Cell(startRow + 2, 2).Value = query.PuestoId?.ToString(CultureInfo.InvariantCulture) ?? "(todos)";

        ws.Cell(startRow + 3, 1).Value = "Activo";
        ws.Cell(startRow + 3, 2).Value = query.Activo.HasValue ? (query.Activo.Value ? "Sí" : "No") : "(todos)";

        ws.Cell(startRow + 4, 1).Value = "EstatusLaboral";
        ws.Cell(startRow + 4, 2).Value = query.EstatusLaboral ?? "(todos)";

        ws.Cell(startRow + 5, 1).Value = "FechaIngresoDesde";
        ws.Cell(startRow + 5, 2).Value = query.FechaIngresoDesde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(sin límite)";

        ws.Cell(startRow + 6, 1).Value = "FechaIngresoHasta";
        ws.Cell(startRow + 6, 2).Value = query.FechaIngresoHasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(sin límite)";

        ws.Cell(startRow + 7, 1).Value = "Search";
        ws.Cell(startRow + 7, 2).Value = query.Search ?? "(vacío)";

        ws.Range(startRow, 1, startRow + 7, 1).Style.Font.Bold = true;
    }

    private static void ComposeFilters(IContainer container, EmpleadosReporteQueryDto query)
    {
        container
            .Border(1)
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(2);

                column.Item().Text(text =>
                {
                    text.Span("SucursalId: ").SemiBold();
                    text.Span(query.SucursalId?.ToString(CultureInfo.InvariantCulture) ?? "(todas)");
                });

                column.Item().Text(text =>
                {
                    text.Span("DepartamentoId: ").SemiBold();
                    text.Span(query.DepartamentoId?.ToString(CultureInfo.InvariantCulture) ?? "(todos)");
                });

                column.Item().Text(text =>
                {
                    text.Span("PuestoId: ").SemiBold();
                    text.Span(query.PuestoId?.ToString(CultureInfo.InvariantCulture) ?? "(todos)");
                });

                column.Item().Text(text =>
                {
                    text.Span("Activo: ").SemiBold();
                    text.Span(query.Activo.HasValue ? (query.Activo.Value ? "Sí" : "No") : "(todos)");
                });

                column.Item().Text(text =>
                {
                    text.Span("EstatusLaboral: ").SemiBold();
                    text.Span(query.EstatusLaboral ?? "(todos)");
                });

                column.Item().Text(text =>
                {
                    text.Span("FechaIngresoDesde: ").SemiBold();
                    text.Span(query.FechaIngresoDesde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(sin límite)");
                });

                column.Item().Text(text =>
                {
                    text.Span("FechaIngresoHasta: ").SemiBold();
                    text.Span(query.FechaIngresoHasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(sin límite)");
                });

                column.Item().Text(text =>
                {
                    text.Span("Search: ").SemiBold();
                    text.Span(query.Search ?? "(vacío)");
                });
            });
    }

    private static void ComposeSummary(IContainer container, IReadOnlyList<EmpleadoExportRowDto> rows)
    {
        var activos = rows.Count(x => x.Activo == "Sí");
        var inactivos = rows.Count(x => x.Activo == "No");

        container
            .Border(1)
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(4);

                column.Item().Text("Resumen").SemiBold().FontSize(11);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Total: {rows.Count}");
                    row.RelativeItem().Text($"Activos: {activos}");
                    row.RelativeItem().Text($"Inactivos: {inactivos}");
                });
            });
    }

    private static void ComposeDetailTable(IContainer container, IReadOnlyList<EmpleadoExportRowDto> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(32);
                columns.ConstantColumn(60);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
                columns.ConstantColumn(58);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(1.4f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("ID").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("NumEmp").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Nombre").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Sucursal").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Depto").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Puesto").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Ingreso").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Telefono").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Email").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Estatus").SemiBold();
            });

            foreach (var item in rows)
            {
                table.Cell().Element(DataCellStyle).Text(item.Id.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(DataCellStyle).Text(item.NumEmpleado);
                table.Cell().Element(DataCellStyle).Text(item.NombreCompleto);
                table.Cell().Element(DataCellStyle).Text(item.Sucursal ?? string.Empty);
                table.Cell().Element(DataCellStyle).Text(item.Departamento ?? string.Empty);
                table.Cell().Element(DataCellStyle).Text(item.Puesto ?? string.Empty);
                table.Cell().Element(DataCellStyle).Text(item.FechaIngreso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                table.Cell().Element(DataCellStyle).Text(item.Telefono ?? string.Empty);
                table.Cell().Element(DataCellStyle).Text(item.Email ?? string.Empty);
                table.Cell().Element(DataCellStyle).Text(item.EstatusLaboral);
            }
        });
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container.Border(1).Padding(3);
    }

    private static IContainer DataCellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderLeft(1).BorderRight(1).Padding(3);
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
}