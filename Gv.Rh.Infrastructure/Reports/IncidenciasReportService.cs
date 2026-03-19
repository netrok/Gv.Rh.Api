using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Incidencias;
using Gv.Rh.Domain.Common.Enums;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
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

        using var workbook = new XLWorkbook();

        BuildIncidenciasSheet(workbook, rows, query);
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
        IncidenciaQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetIncidenciasExportRowsAsync(query, cancellationToken);

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
                        .Text("Reporte de incidencias")
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
            Empleado = string.Join(" ", new[]
            {
                x.Nombres,
                x.ApellidoPaterno,
                x.ApellidoMaterno
            }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Sucursal = x.Sucursal,
            Tipo = x.Tipo.ToString(),
            Estatus = x.Estatus.ToString(),
            FechaInicio = x.FechaInicio,
            FechaFin = x.FechaFin,
            Comentario = x.Comentario,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();
    }

    private static void BuildIncidenciasSheet(
        XLWorkbook workbook,
        IReadOnlyList<IncidenciaExportRowDto> rows,
        IncidenciaQueryDto query)
    {
        var ws = workbook.Worksheets.Add("Incidencias");

        ws.Cell(1, 1).Value = "Reporte de incidencias";
        ws.Cell(2, 1).Value = "Generado UTC";
        ws.Cell(2, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        WriteFilters(ws, query, 4);

        const int headerRow = 12;

        ws.Cell(headerRow, 1).Value = "ID";
        ws.Cell(headerRow, 2).Value = "EmpleadoId";
        ws.Cell(headerRow, 3).Value = "NumEmpleado";
        ws.Cell(headerRow, 4).Value = "Empleado";
        ws.Cell(headerRow, 5).Value = "Sucursal";
        ws.Cell(headerRow, 6).Value = "Tipo";
        ws.Cell(headerRow, 7).Value = "Estatus";
        ws.Cell(headerRow, 8).Value = "FechaInicio";
        ws.Cell(headerRow, 9).Value = "FechaFin";
        ws.Cell(headerRow, 10).Value = "Comentario";
        ws.Cell(headerRow, 11).Value = "Creado UTC";

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = item.Id;
            ws.Cell(rowIndex, 2).Value = item.EmpleadoId;
            ws.Cell(rowIndex, 3).Value = item.NumEmpleado;
            ws.Cell(rowIndex, 4).Value = item.Empleado;
            ws.Cell(rowIndex, 5).Value = item.Sucursal ?? string.Empty;
            ws.Cell(rowIndex, 6).Value = item.Tipo;
            ws.Cell(rowIndex, 7).Value = item.Estatus;
            ws.Cell(rowIndex, 8).Value = item.FechaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(rowIndex, 9).Value = item.FechaFin.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(rowIndex, 10).Value = item.Comentario ?? string.Empty;
            ws.Cell(rowIndex, 11).Value = item.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
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
        IReadOnlyList<IncidenciaExportRowDto> rows,
        IncidenciaQueryDto query)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        ws.Cell(1, 1).Value = "Resumen de incidencias";
        ws.Cell(2, 1).Value = "Generado UTC";
        ws.Cell(2, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        WriteFilters(ws, query, 4);

        ws.Cell(12, 1).Value = "Total";
        ws.Cell(12, 2).Value = rows.Count;

        ws.Cell(14, 1).Value = "Por estatus";
        ws.Cell(15, 1).Value = "Estatus";
        ws.Cell(15, 2).Value = "Cantidad";

        var statusRow = 16;
        foreach (var group in rows.GroupBy(x => x.Estatus).OrderBy(x => x.Key))
        {
            ws.Cell(statusRow, 1).Value = group.Key;
            ws.Cell(statusRow, 2).Value = group.Count();
            statusRow++;
        }

        ws.Cell(14, 4).Value = "Por tipo";
        ws.Cell(15, 4).Value = "Tipo";
        ws.Cell(15, 5).Value = "Cantidad";

        var typeRow = 16;
        foreach (var group in rows.GroupBy(x => x.Tipo).OrderBy(x => x.Key))
        {
            ws.Cell(typeRow, 4).Value = group.Key;
            ws.Cell(typeRow, 5).Value = group.Count();
            typeRow++;
        }

        ws.Range(15, 1, 15, 2).Style.Font.Bold = true;
        ws.Range(15, 4, 15, 5).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }

    private static void WriteFilters(IXLWorksheet ws, IncidenciaQueryDto query, int startRow)
    {
        ws.Cell(startRow, 1).Value = "EmpleadoId";
        ws.Cell(startRow, 2).Value = query.EmpleadoId?.ToString(CultureInfo.InvariantCulture) ?? "(todos)";

        ws.Cell(startRow + 1, 1).Value = "SucursalId";
        ws.Cell(startRow + 1, 2).Value = query.SucursalId?.ToString(CultureInfo.InvariantCulture) ?? "(todas)";

        ws.Cell(startRow + 2, 1).Value = "Tipo";
        ws.Cell(startRow + 2, 2).Value = query.Tipo?.ToString() ?? "(todos)";

        ws.Cell(startRow + 3, 1).Value = "Estatus";
        ws.Cell(startRow + 3, 2).Value = query.Estatus?.ToString() ?? "(todos)";

        ws.Cell(startRow + 4, 1).Value = "FechaDesde";
        ws.Cell(startRow + 4, 2).Value = query.FechaDesde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(sin límite)";

        ws.Cell(startRow + 5, 1).Value = "FechaHasta";
        ws.Cell(startRow + 5, 2).Value = query.FechaHasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(sin límite)";

        ws.Cell(startRow + 6, 1).Value = "SoloPendientes";
        ws.Cell(startRow + 6, 2).Value = query.SoloPendientes ? "Sí" : "No";

        ws.Range(startRow, 1, startRow + 6, 1).Style.Font.Bold = true;
    }

    private static void ComposeFilters(IContainer container, IncidenciaQueryDto query)
    {
        container
            .Border(1)
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(2);

                column.Item().Text(text =>
                {
                    text.Span("EmpleadoId: ").SemiBold();
                    text.Span(query.EmpleadoId?.ToString(CultureInfo.InvariantCulture) ?? "(todos)");
                });

                column.Item().Text(text =>
                {
                    text.Span("SucursalId: ").SemiBold();
                    text.Span(query.SucursalId?.ToString(CultureInfo.InvariantCulture) ?? "(todas)");
                });

                column.Item().Text(text =>
                {
                    text.Span("Tipo: ").SemiBold();
                    text.Span(query.Tipo?.ToString() ?? "(todos)");
                });

                column.Item().Text(text =>
                {
                    text.Span("Estatus: ").SemiBold();
                    text.Span(query.Estatus?.ToString() ?? "(todos)");
                });

                column.Item().Text(text =>
                {
                    text.Span("FechaDesde: ").SemiBold();
                    text.Span(query.FechaDesde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(sin límite)");
                });

                column.Item().Text(text =>
                {
                    text.Span("FechaHasta: ").SemiBold();
                    text.Span(query.FechaHasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(sin límite)");
                });

                column.Item().Text(text =>
                {
                    text.Span("SoloPendientes: ").SemiBold();
                    text.Span(query.SoloPendientes ? "Sí" : "No");
                });
            });
    }

    private static void ComposeSummary(IContainer container, IReadOnlyList<IncidenciaExportRowDto> rows)
    {
        var pendientes = rows.Count(x => x.Estatus == EstatusIncidencia.PENDIENTE.ToString());
        var aprobadas = rows.Count(x => x.Estatus == EstatusIncidencia.APROBADA.ToString());
        var rechazadas = rows.Count(x => x.Estatus == EstatusIncidencia.RECHAZADA.ToString());

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
                    row.RelativeItem().Text($"Pendientes: {pendientes}");
                    row.RelativeItem().Text($"Aprobadas: {aprobadas}");
                    row.RelativeItem().Text($"Rechazadas: {rechazadas}");
                });
            });
    }

    private static void ComposeDetailTable(IContainer container, IReadOnlyList<IncidenciaExportRowDto> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);
                columns.ConstantColumn(58);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("ID").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("NumEmp").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Empleado").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Sucursal").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Tipo").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Estatus").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Inicio").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Fin").SemiBold();
                header.Cell().Element(HeaderCellStyle).Text("Comentario").SemiBold();
            });

            foreach (var item in rows)
            {
                table.Cell().Element(DataCellStyle).Text(item.Id.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(DataCellStyle).Text(item.NumEmpleado);
                table.Cell().Element(DataCellStyle).Text(item.Empleado);
                table.Cell().Element(DataCellStyle).Text(item.Sucursal ?? string.Empty);
                table.Cell().Element(DataCellStyle).Text(item.Tipo);
                table.Cell().Element(DataCellStyle).Text(item.Estatus);
                table.Cell().Element(DataCellStyle).Text(item.FechaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                table.Cell().Element(DataCellStyle).Text(item.FechaFin.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                table.Cell().Element(DataCellStyle).Text(item.Comentario ?? string.Empty);
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

    private static string BuildFileName(IncidenciaQueryDto query, string extension)
    {
        var parts = new List<string>
        {
            "incidencias",
            DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
        };

        if (query.SoloPendientes)
            parts.Add("pendientes");

        if (query.SucursalId.HasValue)
            parts.Add($"sucursal-{query.SucursalId.Value}");

        if (query.EmpleadoId.HasValue)
            parts.Add($"empleado-{query.EmpleadoId.Value}");

        return string.Join("_", parts) + "." + extension;
    }
}