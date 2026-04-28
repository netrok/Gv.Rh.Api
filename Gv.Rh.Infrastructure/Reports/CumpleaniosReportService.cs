using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Application.DTOs.Cumpleanios;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace Gv.Rh.Infrastructure.Reports;

public sealed class CumpleaniosReportService : ICumpleaniosReportService
{
    private readonly RhDbContext _context;

    public CumpleaniosReportService(RhDbContext context)
    {
        _context = context;
    }

    public async Task<ReportFileDto> BuildXlsxAsync(
        CumpleaniosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetCumpleaniosExportRowsAsync(query, cancellationToken);
        var labels = await ResolveFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        using var workbook = new XLWorkbook();
        BuildResumenSheet(workbook, rows, labels, generatedAtUtc);
        BuildDetalleSheet(workbook, rows, labels, generatedAtUtc);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ReportFileDto
        {
            Content = stream.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = BuildFileName(labels.ScopeNormalizado, "xlsx")
        };
    }

    public async Task<ReportFileDto> BuildPdfAsync(
        CumpleaniosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetCumpleaniosExportRowsAsync(query, cancellationToken);
        var labels = await ResolveFilterLabelsAsync(query, cancellationToken);
        var generatedAtUtc = DateTime.UtcNow;

        var pdfBytes = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.BlueGrey.Darken4));

                page.Header().Element(container => ComposeHeader(container, labels, generatedAtUtc));

                page.Content().PaddingVertical(8).Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(container => ComposeFilters(container, labels));
                    column.Item().Element(container => ComposeKpis(container, rows));
                    column.Item().Element(container => ComposeDistribution(container, rows));
                    column.Item().Element(container => ComposeDetail(container, rows));
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generado: ");
                    text.Span(FormatDateTime(generatedAtUtc)).SemiBold();
                    text.Span(" · Página ");
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
            FileName = BuildFileName(labels.ScopeNormalizado, "pdf")
        };
    }

    private async Task<List<CumpleaniosExportRowDto>> GetCumpleaniosExportRowsAsync(
        CumpleaniosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var periodo = ResolvePeriodo(query, hoy);

        var baseItems = await BuildBaseQuery(query)
            .OrderBy(x => x.Nombres ?? string.Empty)
            .ThenBy(x => x.ApellidoPaterno ?? string.Empty)
            .ThenBy(x => x.ApellidoMaterno ?? string.Empty)
            .ToListAsync(cancellationToken);

        var rows = baseItems
            .Where(x => x.FechaNacimiento.HasValue)
            .Select(x =>
            {
                var fechaNacimiento = x.FechaNacimiento!.Value;
                var proximoCumple = GetProximoCumple(fechaNacimiento, hoy);

                return new CumpleaniosExportRowDto
                {
                    EmpleadoId = x.EmpleadoId,
                    NumEmpleado = x.NumEmpleado ?? string.Empty,
                    Empleado = CombineFullName(x.Nombres, x.ApellidoPaterno, x.ApellidoMaterno),
                    FechaNacimiento = fechaNacimiento,
                    ProximoCumple = proximoCumple,
                    EdadQueCumple = GetEdadQueCumple(fechaNacimiento, proximoCumple),
                    DiasFaltantes = proximoCumple.DayNumber - hoy.DayNumber,
                    Puesto = x.Puesto,
                    Departamento = x.Departamento,
                    Sucursal = x.Sucursal,
                    Email = x.Email,
                    Telefono = x.Telefono
                };
            })
            .Where(x => x.ProximoCumple >= periodo.Desde && x.ProximoCumple <= periodo.Hasta)
            .OrderBy(x => x.ProximoCumple)
            .ThenBy(x => x.Empleado)
            .ToList();

        return rows;
    }

    private IQueryable<CumpleaniosQueryItem> BuildBaseQuery(CumpleaniosReporteQueryDto query)
    {
        var queryable = _context.Empleados
            .AsNoTracking()
            .Where(x => x.Activo)
            .Select(x => new CumpleaniosQueryItem
            {
                EmpleadoId = x.Id,
                NumEmpleado = x.NumEmpleado,
                Nombres = x.Nombres,
                ApellidoPaterno = x.ApellidoPaterno,
                ApellidoMaterno = x.ApellidoMaterno,
                FechaNacimiento = x.FechaNacimiento,
                Telefono = x.Telefono,
                Email = x.Email,
                DepartamentoId = x.DepartamentoId,
                Departamento = x.Departamento != null ? x.Departamento.Nombre : null,
                Puesto = x.Puesto != null ? x.Puesto.Nombre : null,
                SucursalId = x.SucursalId,
                Sucursal = x.Sucursal != null ? x.Sucursal.Nombre : null
            });

        if (query.DepartamentoId.HasValue)
        {
            queryable = queryable.Where(x => x.DepartamentoId == query.DepartamentoId.Value);
        }

        if (query.SucursalId.HasValue)
        {
            queryable = queryable.Where(x => x.SucursalId == query.SucursalId.Value);
        }

        return queryable;
    }

    private async Task<ReportLabels> ResolveFilterLabelsAsync(
        CumpleaniosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var periodo = ResolvePeriodo(query, hoy);

        var sucursal = "Todas";
        if (query.SucursalId.HasValue)
        {
            sucursal = await _context.Sucursales
                .AsNoTracking()
                .Where(x => x.Id == query.SucursalId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken)
                ?? $"Sucursal #{query.SucursalId.Value}";
        }

        var departamento = "Todos";
        if (query.DepartamentoId.HasValue)
        {
            departamento = await _context.Departamentos
                .AsNoTracking()
                .Where(x => x.Id == query.DepartamentoId.Value)
                .Select(x => x.Nombre)
                .FirstOrDefaultAsync(cancellationToken)
                ?? $"Departamento #{query.DepartamentoId.Value}";
        }

        return new ReportLabels
        {
            Sucursal = sucursal,
            Departamento = departamento,
            ScopeNormalizado = periodo.ScopeNormalizado,
            PeriodoLabel = BuildPeriodoLabel(periodo.Desde, periodo.Hasta, periodo.ScopeNormalizado)
        };
    }

    private static (DateOnly Desde, DateOnly Hasta, string ScopeNormalizado) ResolvePeriodo(
        CumpleaniosReporteQueryDto query,
        DateOnly hoy)
    {
        var scope = (query.Scope ?? "30dias").Trim().ToLowerInvariant();

        return scope switch
        {
            "hoy" => (hoy, hoy, "hoy"),
            "7dias" => (hoy, hoy.AddDays(7), "7dias"),
            "30dias" => (hoy, hoy.AddDays(30), "30dias"),
            "mes" => (
                new DateOnly(hoy.Year, hoy.Month, 1),
                new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month)),
                "mes"
            ),
            "custom" when query.FechaDesde.HasValue && query.FechaHasta.HasValue
                => (query.FechaDesde.Value, query.FechaHasta.Value, "custom"),
            _ => (hoy, hoy.AddDays(30), "30dias")
        };
    }

    private static DateOnly GetProximoCumple(DateOnly fechaNacimiento, DateOnly hoy)
    {
        var year = hoy.Year;
        var month = fechaNacimiento.Month;
        var day = fechaNacimiento.Day;

        var cumpleEsteAnio = CreateSafeDate(year, month, day);

        return cumpleEsteAnio < hoy
            ? CreateSafeDate(year + 1, month, day)
            : cumpleEsteAnio;
    }

    private static DateOnly CreateSafeDate(int year, int month, int day)
    {
        var safeDay = Math.Min(day, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, safeDay);
    }

    private static int GetEdadQueCumple(DateOnly fechaNacimiento, DateOnly proximoCumple)
    {
        return proximoCumple.Year - fechaNacimiento.Year;
    }

    private static string CombineFullName(
        string? nombres,
        string? apellidoPaterno,
        string? apellidoMaterno)
    {
        return string.Join(
            " ",
            new[] { nombres, apellidoPaterno, apellidoMaterno }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
    }

    private static string BuildPeriodoLabel(
        DateOnly desde,
        DateOnly hasta,
        string scopeNormalizado)
    {
        return scopeNormalizado switch
        {
            "hoy" => "Hoy",
            "7dias" => $"Próximos 7 días ({FormatDate(desde)} al {FormatDate(hasta)})",
            "30dias" => $"Próximos 30 días ({FormatDate(desde)} al {FormatDate(hasta)})",
            "mes" => $"Mes actual ({FormatDate(desde)} al {FormatDate(hasta)})",
            "custom" => $"Personalizado ({FormatDate(desde)} al {FormatDate(hasta)})",
            _ => $"{FormatDate(desde)} al {FormatDate(hasta)}"
        };
    }

    private static string BuildFileName(string scope, string extension)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return $"cumpleanios_{scope}_{stamp}.{extension}";
    }

    private static string FormatDate(DateOnly? value)
    {
        return value.HasValue
            ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : "—";
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        IReadOnlyCollection<CumpleaniosExportRowDto> rows,
        ReportLabels labels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Resumen");
        var row = 1;

        ws.Cell(row, 1).Value = "Reporte de cumpleaños";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 16;
        row++;

        ws.Cell(row, 1).Value = "Generado";
        ws.Cell(row, 2).Value = FormatDateTime(generatedAtUtc);
        row++;

        ws.Cell(row, 1).Value = "Sucursal";
        ws.Cell(row, 2).Value = labels.Sucursal;
        row++;

        ws.Cell(row, 1).Value = "Departamento";
        ws.Cell(row, 2).Value = labels.Departamento;
        row++;

        ws.Cell(row, 1).Value = "Periodo";
        ws.Cell(row, 2).Value = labels.PeriodoLabel;
        row += 2;

        var hoyCount = rows.Count(x => x.DiasFaltantes == 0);
        var sevenDaysCount = rows.Count(x => x.DiasFaltantes >= 0 && x.DiasFaltantes <= 7);
        var monthCount = rows.Count(x => x.ProximoCumple.Month == DateTime.Today.Month);

        ws.Cell(row, 1).Value = "KPI";
        ws.Cell(row, 2).Value = "Valor";
        ApplyHeader(ws, row, 1, 2);
        row++;

        ws.Cell(row, 1).Value = "Total en periodo";
        ws.Cell(row, 2).Value = rows.Count;
        row++;

        ws.Cell(row, 1).Value = "Cumpleaños hoy";
        ws.Cell(row, 2).Value = hoyCount;
        row++;

        ws.Cell(row, 1).Value = "Próximos 7 días";
        ws.Cell(row, 2).Value = sevenDaysCount;
        row++;

        ws.Cell(row, 1).Value = "Este mes";
        ws.Cell(row, 2).Value = monthCount;
        row += 2;

        ws.Cell(row, 1).Value = "Distribución por sucursal";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        ws.Cell(row, 1).Value = "Sucursal";
        ws.Cell(row, 2).Value = "Total";
        ApplyHeader(ws, row, 1, 2);
        row++;

        foreach (var item in rows
                     .GroupBy(x => string.IsNullOrWhiteSpace(x.Sucursal) ? "Sin sucursal" : x.Sucursal!)
                     .Select(x => new { Nombre = x.Key, Total = x.Count() })
                     .OrderByDescending(x => x.Total)
                     .ThenBy(x => x.Nombre))
        {
            ws.Cell(row, 1).Value = item.Nombre;
            ws.Cell(row, 2).Value = item.Total;
            row++;
        }

        row++;
        ws.Cell(row, 1).Value = "Distribución por departamento";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        ws.Cell(row, 1).Value = "Departamento";
        ws.Cell(row, 2).Value = "Total";
        ApplyHeader(ws, row, 1, 2);
        row++;

        foreach (var item in rows
                     .GroupBy(x => string.IsNullOrWhiteSpace(x.Departamento) ? "Sin departamento" : x.Departamento!)
                     .Select(x => new { Nombre = x.Key, Total = x.Count() })
                     .OrderByDescending(x => x.Total)
                     .ThenBy(x => x.Nombre))
        {
            ws.Cell(row, 1).Value = item.Nombre;
            ws.Cell(row, 2).Value = item.Total;
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void BuildDetalleSheet(
        XLWorkbook workbook,
        IReadOnlyCollection<CumpleaniosExportRowDto> rows,
        ReportLabels labels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Cumpleanios");
        var row = 1;

        ws.Cell(row, 1).Value = "Reporte de cumpleaños - detalle";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        row++;

        ws.Cell(row, 1).Value = "Generado";
        ws.Cell(row, 2).Value = FormatDateTime(generatedAtUtc);
        ws.Cell(row, 4).Value = "Periodo";
        ws.Cell(row, 5).Value = labels.PeriodoLabel;
        row += 2;

        var headerRow = row;
        var headers = new[]
        {
            "Núm. Emp.",
            "Empleado",
            "Fecha nacimiento",
            "Próximo cumple",
            "Edad que cumple",
            "Días faltantes",
            "Puesto",
            "Departamento",
            "Sucursal",
            "Correo",
            "Teléfono"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(headerRow, i + 1).Value = headers[i];
        }

        ApplyHeader(ws, headerRow, 1, headers.Length);
        row++;

        foreach (var item in rows)
        {
            ws.Cell(row, 1).Value = item.NumEmpleado;
            ws.Cell(row, 2).Value = item.Empleado;
            ws.Cell(row, 3).Value = FormatDate(item.FechaNacimiento);
            ws.Cell(row, 4).Value = FormatDate(item.ProximoCumple);
            ws.Cell(row, 5).Value = item.EdadQueCumple;
            ws.Cell(row, 6).Value = item.DiasFaltantes;
            ws.Cell(row, 7).Value = item.Puesto ?? string.Empty;
            ws.Cell(row, 8).Value = item.Departamento ?? string.Empty;
            ws.Cell(row, 9).Value = item.Sucursal ?? string.Empty;
            ws.Cell(row, 10).Value = item.Email ?? string.Empty;
            ws.Cell(row, 11).Value = item.Telefono ?? string.Empty;
            row++;
        }

        ws.SheetView.FreezeRows(headerRow);
        ws.Columns().AdjustToContents();
    }

    private static void ApplyHeader(IXLWorksheet ws, int row, int fromColumn, int toColumn)
    {
        var range = ws.Range(row, fromColumn, row, toColumn);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static void ComposeHeader(
        IContainer container,
        ReportLabels labels,
        DateTime generatedAtUtc)
    {
        container.BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingBottom(6)
            .Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Reporte de cumpleaños")
                        .FontSize(18)
                        .SemiBold()
                        .FontColor(Colors.Blue.Darken3);

                    column.Item().Text("Seguimiento de celebraciones del personal")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken2);
                });

                row.ConstantItem(220).AlignRight().Column(column =>
                {
                    column.Item().Text($"Periodo: {labels.PeriodoLabel}")
                        .FontSize(9)
                        .SemiBold();

                    column.Item().Text($"Generado: {FormatDateTime(generatedAtUtc)}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });
            });
    }

    private static void ComposeFilters(IContainer container, ReportLabels labels)
    {
        container.Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(4);

                column.Item().Text("Filtros aplicados")
                    .SemiBold()
                    .FontColor(Colors.Blue.Darken3);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Sucursal: {labels.Sucursal}");
                    row.RelativeItem().Text($"Departamento: {labels.Departamento}");
                    row.RelativeItem().Text($"Periodo: {labels.PeriodoLabel}");
                });
            });
    }

    private static void ComposeKpis(
        IContainer container,
        IReadOnlyCollection<CumpleaniosExportRowDto> rows)
    {
        var hoyCount = rows.Count(x => x.DiasFaltantes == 0);
        var sevenDaysCount = rows.Count(x => x.DiasFaltantes >= 0 && x.DiasFaltantes <= 7);
        var monthCount = rows.Count(x => x.ProximoCumple.Month == DateTime.Today.Month);

        container.Row(row =>
        {
            row.Spacing(8);

            row.RelativeItem().Element(c => ComposeKpiCard(c, "Total en periodo", rows.Count.ToString(), Colors.Blue.Darken3));
            row.RelativeItem().Element(c => ComposeKpiCard(c, "Hoy", hoyCount.ToString(), Colors.Green.Darken2));
            row.RelativeItem().Element(c => ComposeKpiCard(c, "Próximos 7 días", sevenDaysCount.ToString(), Colors.Orange.Darken2));
            row.RelativeItem().Element(c => ComposeKpiCard(c, "Este mes", monthCount.ToString(), Colors.Purple.Darken2));
        });
    }

    private static void ComposeKpiCard(
        IContainer container,
        string title,
        string value,
        string accentColor)
    {
        container.Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(3);
                column.Item().Text(title).FontSize(8).FontColor(Colors.Grey.Darken2);
                column.Item().Text(value).FontSize(18).SemiBold().FontColor(accentColor);
            });
    }

    private static void ComposeDistribution(
        IContainer container,
        IReadOnlyCollection<CumpleaniosExportRowDto> rows)
    {
        var bySucursal = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Sucursal) ? "Sin sucursal" : x.Sucursal!)
            .Select(x => new { Nombre = x.Key, Total = x.Count() })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Nombre)
            .Take(8)
            .ToList();

        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("Distribución por sucursal")
                .SemiBold()
                .FontColor(Colors.Blue.Darken3);

            if (bySucursal.Count == 0)
            {
                column.Item().Text("Sin información para mostrar.")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(70);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Sucursal");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total");
                });

                foreach (var item in bySucursal)
                {
                    table.Cell().Element(BodyCellStyle).Text(item.Nombre);
                    table.Cell().Element(BodyCellStyle).AlignRight().Text(item.Total.ToString(CultureInfo.InvariantCulture));
                }
            });
        });
    }

    private static void ComposeDetail(
        IContainer container,
        IReadOnlyCollection<CumpleaniosExportRowDto> rows)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("Detalle")
                .SemiBold()
                .FontColor(Colors.Blue.Darken3);

            if (rows.Count == 0)
            {
                column.Item().Text("No hay cumpleaños para el periodo seleccionado.")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(60);
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(65);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Núm.");
                    header.Cell().Element(HeaderCellStyle).Text("Empleado");
                    header.Cell().Element(HeaderCellStyle).Text("Próximo");
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Edad");
                    header.Cell().Element(HeaderCellStyle).Text("Departamento");
                    header.Cell().Element(HeaderCellStyle).Text("Sucursal");
                });

                foreach (var item in rows)
                {
                    table.Cell().Element(BodyCellStyle).Text(item.NumEmpleado);
                    table.Cell().Element(BodyCellStyle).Text(item.Empleado);
                    table.Cell().Element(BodyCellStyle).Text(FormatDate(item.ProximoCumple));
                    table.Cell().Element(BodyCellStyle).AlignRight().Text(item.EdadQueCumple.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(BodyCellStyle).Text(item.Departamento ?? "—");
                    table.Cell().Element(BodyCellStyle).Text(item.Sucursal ?? "—");
                }
            });
        });
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Blue.Lighten5)
            .PaddingVertical(5)
            .PaddingHorizontal(4);
    }

    private static IContainer BodyCellStyle(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(4)
            .PaddingHorizontal(4);
    }

    private sealed class CumpleaniosQueryItem
    {
        public int EmpleadoId { get; set; }
        public string? NumEmpleado { get; set; }
        public string? Nombres { get; set; }
        public string? ApellidoPaterno { get; set; }
        public string? ApellidoMaterno { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public int? DepartamentoId { get; set; }
        public string? Departamento { get; set; }
        public string? Puesto { get; set; }
        public int? SucursalId { get; set; }
        public string? Sucursal { get; set; }
    }

    private sealed class ReportLabels
    {
        public string Sucursal { get; set; } = "Todas";
        public string Departamento { get; set; } = "Todos";
        public string PeriodoLabel { get; set; } = string.Empty;
        public string ScopeNormalizado { get; set; } = "30dias";
    }
}