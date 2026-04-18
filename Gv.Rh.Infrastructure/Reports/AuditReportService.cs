using ClosedXML.Excel;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Audit;
using Gv.Rh.Application.DTOs.Common;
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
using System.Text.Json;

namespace Gv.Rh.Infrastructure.Reports;

public sealed class AuditReportService : IAuditReportService
{
    private readonly RhDbContext _context;

    public AuditReportService(RhDbContext context)
    {
        _context = context;
    }

    public async Task<ReportFileDto> BuildXlsxAsync(
        AuditReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetAuditRowsAsync(query, cancellationToken);
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
        AuditReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var rows = await GetAuditRowsAsync(query, cancellationToken);
        var filterLabels = ResolveFilterLabels(query);
        var generatedAtUtc = DateTime.UtcNow;

        var total = rows.Count;
        var usuarios = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.UserEmail, "(sin usuario)"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var entidades = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.EntityName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var creates = rows.Count(x => NormalizeKey(x.Action) == "CREATE");
        var updates = rows.Count(x => NormalizeKey(x.Action) == "UPDATE");
        var eliminaciones = rows.Count(x =>
            NormalizeKey(x.Action) is "DELETE" or "SOFTDELETE");

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
                        "Reporte de auditoría",
                        "Bitácora institucional",
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
                            ("Eventos", total.ToString(CultureInfo.InvariantCulture), "Registros visibles", CorporateReportPalette.KpiPrimary),
                            ("Usuarios", usuarios.ToString(CultureInfo.InvariantCulture), "Actores distintos", CorporateReportPalette.KpiTeal),
                            ("Entidades", entidades.ToString(CultureInfo.InvariantCulture), "Módulos tocados", CorporateReportPalette.KpiPurple),
                            ("Altas", creates.ToString(CultureInfo.InvariantCulture), "CREATE", CorporateReportPalette.KpiSuccess),
                            ("Cambios", updates.ToString(CultureInfo.InvariantCulture), "UPDATE", CorporateReportPalette.KpiWarning)));

                    column.Item().Element(container => ComposeDistributionSection(container, rows));
                    column.Item().Element(container => ComposeDetailSection(container, rows));
                });

                page.Footer().Element(container =>
                    CorporatePdfBlocks.ComposeReportFooter(
                        container,
                        generatedAtUtc,
                        $"Emitido: {CorporateReportFormatters.FormatUtcStamp(generatedAtUtc)} · Eliminaciones: {eliminaciones}"));
            });
        }).GeneratePdf();

        return new ReportFileDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = BuildFileName(query, "pdf")
        };
    }

    private IQueryable<AuditLog> BuildBaseQuery(AuditReportQueryDto query)
    {
        var qset = _context.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.EntityName))
            qset = qset.Where(x => x.EntityName == query.EntityName.Trim());

        if (!string.IsNullOrWhiteSpace(query.RecordId))
            qset = qset.Where(x => x.RecordId == query.RecordId.Trim());

        if (!string.IsNullOrWhiteSpace(query.Action))
            qset = qset.Where(x => x.Action == query.Action.Trim());

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            var term = query.Email.Trim();
            qset = qset.Where(x =>
                x.UserEmail != null &&
                EF.Functions.ILike(x.UserEmail, $"%{term}%"));
        }

        if (query.From.HasValue)
            qset = qset.Where(x => x.OccurredAtUtc >= ToUtcFrom(query.From.Value));

        if (query.To.HasValue)
            qset = qset.Where(x => x.OccurredAtUtc <= ToUtcToInclusive(query.To.Value));

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();

            qset = qset.Where(x =>
                (x.UserEmail != null && EF.Functions.ILike(x.UserEmail, $"%{term}%")) ||
                (x.UserRole != null && EF.Functions.ILike(x.UserRole, $"%{term}%")) ||
                EF.Functions.ILike(x.EntityName, $"%{term}%") ||
                EF.Functions.ILike(x.Action, $"%{term}%") ||
                EF.Functions.ILike(x.RecordId, $"%{term}%") ||
                (x.IpAddress != null && EF.Functions.ILike(x.IpAddress, $"%{term}%")) ||
                (x.OldValuesJson != null && EF.Functions.ILike(x.OldValuesJson, $"%{term}%")) ||
                (x.NewValuesJson != null && EF.Functions.ILike(x.NewValuesJson, $"%{term}%")) ||
                (x.ChangedColumnsJson != null && EF.Functions.ILike(x.ChangedColumnsJson, $"%{term}%"))
            );
        }

        return qset;
    }

    private async Task<List<AuditExportRow>> GetAuditRowsAsync(
        AuditReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var rawRows = await BuildBaseQuery(query)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(50000)
            .Select(x => new
            {
                x.Id,
                x.OccurredAtUtc,
                x.UserId,
                x.UserEmail,
                x.UserRole,
                x.Action,
                x.EntityName,
                x.RecordId,
                x.IpAddress,
                x.UserAgent,
                x.OldValuesJson,
                x.NewValuesJson,
                x.ChangedColumnsJson
            })
            .ToListAsync(cancellationToken);

        return rawRows.Select(x => new AuditExportRow
        {
            Id = x.Id,
            OccurredAtUtc = x.OccurredAtUtc,
            UserId = x.UserId,
            UserEmail = x.UserEmail,
            UserRole = x.UserRole,
            Action = x.Action,
            EntityName = x.EntityName,
            RecordId = x.RecordId,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            OldValuesJson = x.OldValuesJson,
            NewValuesJson = x.NewValuesJson,
            ChangedColumnsJson = x.ChangedColumnsJson,
            ChangedColumnsSummary = SummarizeChangedColumns(x.ChangedColumnsJson)
        }).ToList();
    }

    private static AuditReportFilterLabels ResolveFilterLabels(AuditReportQueryDto query)
    {
        return new AuditReportFilterLabels
        {
            EntityName = CorporateReportFormatters.FormatFilterValue(query.EntityName),
            RecordId = CorporateReportFormatters.FormatFilterValue(query.RecordId),
            Action = CorporateReportFormatters.FormatFilterValue(
                string.IsNullOrWhiteSpace(query.Action)
                    ? null
                    : CorporateReportFormatters.FormatLabel(query.Action),
                "(todas)"),
            Email = CorporateReportFormatters.FormatFilterValue(query.Email),
            Periodo = BuildPeriodLabel(query.From, query.To),
            Search = CorporateReportFormatters.FormatSearchTerm(query.Q)
        };
    }

    private static IReadOnlyList<(string Label, string Value)> BuildFilterItems(AuditReportFilterLabels labels)
    {
        return
        [
            ("Entidad", labels.EntityName),
            ("Registro", labels.RecordId),
            ("Acción", labels.Action),
            ("Correo", labels.Email),
            ("Periodo", labels.Periodo),
            ("Búsqueda", labels.Search)
        ];
    }

    private static void BuildResumenSheet(
        XLWorkbook workbook,
        IReadOnlyList<AuditExportRow> rows,
        AuditReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de auditoría",
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
        var usuarios = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.UserEmail, "(sin usuario)"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var entidades = rows
            .Select(x => CorporateReportFormatters.NullSafe(x.EntityName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var creates = rows.Count(x => NormalizeKey(x.Action) == "CREATE");
        var updates = rows.Count(x => NormalizeKey(x.Action) == "UPDATE");

        nextRow = CorporateExcelStyles.WriteKpiCards(
            ws,
            nextRow,
            new (string Title, string Value, string AccentColor)[]
            {
                ("Eventos", total.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPrimary),
                ("Usuarios", usuarios.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiTeal),
                ("Entidades", entidades.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiPurple),
                ("Altas", creates.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiSuccess),
                ("Cambios", updates.ToString(CultureInfo.InvariantCulture), CorporateReportPalette.KpiWarning)
            });

        nextRow += 2;

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 1,
            sectionTitle: "Por acción",
            header1: "Acción",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.FormatLabel(x.Action))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 4,
            sectionTitle: "Por entidad",
            header1: "Entidad",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.NullSafe(x.EntityName))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .ToList());

        WriteSummaryTable(
            ws,
            startRow: nextRow,
            startColumn: 7,
            sectionTitle: "Por usuario",
            header1: "Usuario",
            header2: "Cantidad",
            rows: rows
                .GroupBy(x => CorporateReportFormatters.NullSafe(x.UserEmail, "(sin usuario)"))
                .OrderBy(x => x.Key)
                .Select(x => (x.Key, x.Count()))
                .Take(12)
                .ToList());

        CorporateExcelStyles.SetColumnWidths(ws, 24, 18, 4, 24, 18, 4, 28, 18, 4, 16);
    }

    private static void BuildDetalleSheet(
        XLWorkbook workbook,
        IReadOnlyList<AuditExportRow> rows,
        AuditReportFilterLabels filterLabels,
        DateTime generatedAtUtc)
    {
        var ws = workbook.Worksheets.Add("Audit");

        CorporateExcelStyles.WriteReportHeader(
            ws,
            "Reporte de auditoría",
            "Detalle operativo",
            generatedAtUtc,
            startColumn: 1,
            endColumn: 12,
            startRow: 1);

        var nextRow = CorporateExcelStyles.WriteFiltersBlock(
            ws,
            startRow: 5,
            filters: BuildFilterItems(filterLabels));

        nextRow += 2;

        var headerRow = nextRow;

        ws.Cell(headerRow, 1).Value = "Id";
        ws.Cell(headerRow, 2).Value = "OccurredAtUtc";
        ws.Cell(headerRow, 3).Value = "UserEmail";
        ws.Cell(headerRow, 4).Value = "UserRole";
        ws.Cell(headerRow, 5).Value = "Action";
        ws.Cell(headerRow, 6).Value = "EntityName";
        ws.Cell(headerRow, 7).Value = "RecordId";
        ws.Cell(headerRow, 8).Value = "IpAddress";
        ws.Cell(headerRow, 9).Value = "UserAgent";
        ws.Cell(headerRow, 10).Value = "OldValuesJson";
        ws.Cell(headerRow, 11).Value = "NewValuesJson";
        ws.Cell(headerRow, 12).Value = "ChangedColumnsJson";

        CorporateExcelStyles.ApplyTableHeader(ws.Range(headerRow, 1, headerRow, 12));

        var rowIndex = headerRow + 1;

        foreach (var item in rows)
        {
            ws.Cell(rowIndex, 1).Value = item.Id;
            ws.Cell(rowIndex, 2).Value = CorporateReportFormatters.ExcelDateTime(item.OccurredAtUtc);
            ws.Cell(rowIndex, 3).Value = CorporateReportFormatters.ExcelText(item.UserEmail);
            ws.Cell(rowIndex, 4).Value = CorporateReportFormatters.ExcelText(item.UserRole);
            ws.Cell(rowIndex, 5).Value = CorporateReportFormatters.ExcelText(item.Action);
            ws.Cell(rowIndex, 6).Value = CorporateReportFormatters.ExcelText(item.EntityName);
            ws.Cell(rowIndex, 7).Value = CorporateReportFormatters.ExcelText(item.RecordId);
            ws.Cell(rowIndex, 8).Value = CorporateReportFormatters.ExcelText(item.IpAddress);
            ws.Cell(rowIndex, 9).Value = CorporateReportFormatters.ExcelText(item.UserAgent);
            ws.Cell(rowIndex, 10).Value = CorporateReportFormatters.ExcelText(item.OldValuesJson, string.Empty);
            ws.Cell(rowIndex, 11).Value = CorporateReportFormatters.ExcelText(item.NewValuesJson, string.Empty);
            ws.Cell(rowIndex, 12).Value = CorporateReportFormatters.ExcelText(item.ChangedColumnsJson, string.Empty);
            rowIndex++;
        }

        var lastDataRow = rowIndex - 1;

        CorporateExcelStyles.ApplyDataGrid(ws, headerRow + 1, lastDataRow, 1, 12);
        CorporateExcelStyles.FinalizeTable(ws, headerRow, 1, 12, lastDataRow);

        CorporateExcelStyles.SetColumnWidths(ws,
            10, // Id
            22, // OccurredAtUtc
            28, // UserEmail
            14, // UserRole
            14, // Action
            18, // EntityName
            14, // RecordId
            16, // IpAddress
            30, // UserAgent
            42, // Old
            42, // New
            28  // Changed
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
        IReadOnlyList<AuditExportRow> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Distribución", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(12);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);

                    left.Item().Text("Por acción")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.FormatLabel(x.Action))
                        .OrderBy(x => x.Key)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        left.Item().Text("Sin eventos para resumir.")
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

                    right.Item().Text("Por entidad")
                        .FontSize(9.5f)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink700);

                    var groups = rows
                        .GroupBy(x => CorporateReportFormatters.NullSafe(x.EntityName))
                        .OrderBy(x => x.Key)
                        .ToList();

                    if (groups.Count == 0)
                    {
                        right.Item().Text("Sin eventos para resumir.")
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
        IReadOnlyList<AuditExportRow> rows)
    {
        CorporatePdfBlocks.ComposeSection(container, "Detalle de eventos", body =>
        {
            if (rows.Count == 0)
            {
                body.Item().Element(c =>
                    CorporatePdfBlocks.ComposeEmptyState(c, "No se encontraron eventos con los filtros aplicados."));
                return;
            }

            body.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(84);     // Fecha UTC
                    columns.RelativeColumn(1.8f);   // Usuario
                    columns.ConstantColumn(58);     // Acción
                    columns.ConstantColumn(72);     // Entidad
                    columns.ConstantColumn(58);     // Registro
                    columns.ConstantColumn(68);     // IP
                    columns.RelativeColumn(2.3f);   // Cambios
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Fecha UTC");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Usuario");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Acción");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Entidad");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Registro");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("IP");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Cambios");
                });

                for (var index = 0; index < rows.Count; index++)
                {
                    var item = rows[index];
                    var background = CorporatePdfStyles.ZebraRow(index);

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDateTime(item.OccurredAtUtc));

                    table.Cell().Element(c => c.TableDataCell(background, emphasize: true))
                        .Text(CorporateReportFormatters.NullSafe(item.UserEmail, "(sin usuario)"));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatLabel(item.Action));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.EntityName));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.RecordId));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.IpAddress));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.NullSafe(item.ChangedColumnsSummary));
                }
            });
        });
    }

    private static string BuildFileName(AuditReportQueryDto query, string extension)
    {
        return CorporateReportFormatters.BuildTimestampedFileName(
            "audit",
            extension,
            BuildDateSegment(query.From, query.To),
            query.EntityName,
            query.Action);
    }

    private static string BuildDateSegment(DateTime? from, DateTime? to)
    {
        if (!from.HasValue && !to.HasValue)
            return string.Empty;

        var f = (from ?? DateTime.UtcNow).Date;
        var t = (to ?? DateTime.UtcNow).Date;
        return $"{f:yyyyMMdd}_{t:yyyyMMdd}";
    }

    private static string BuildPeriodLabel(DateTime? from, DateTime? to)
    {
        if (!from.HasValue && !to.HasValue)
            return "(sin límite)";

        if (from.HasValue && to.HasValue)
            return $"{from.Value:dd/MM/yyyy} al {to.Value:dd/MM/yyyy}";

        if (from.HasValue)
            return $"Desde {from.Value:dd/MM/yyyy}";

        return $"Hasta {to!.Value:dd/MM/yyyy}";
    }

    private static DateTime ToUtcFrom(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
            return dt;

        if (dt.Kind == DateTimeKind.Unspecified)
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);

        return dt.ToUniversalTime();
    }

    private static DateTime ToUtcToInclusive(DateTime dt)
    {
        var value = dt;

        if (dt.TimeOfDay == TimeSpan.Zero)
            value = dt.Date.AddDays(1).AddTicks(-1);

        return ToUtcFrom(value);
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToUpperInvariant();
    }

    private static string SummarizeChangedColumns(string? changedColumnsJson)
    {
        if (string.IsNullOrWhiteSpace(changedColumnsJson))
            return "Sin detalle";

        try
        {
            using var doc = JsonDocument.Parse(changedColumnsJson);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var items = doc.RootElement
                    .EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim())
                    .ToList();

                if (items.Count == 0)
                    return "Sin detalle";

                var joined = string.Join(", ", items);
                return joined.Length > 120 ? joined[..120] + "..." : joined;
            }
        }
        catch
        {
            // ignora y regresa raw truncado
        }

        var raw = changedColumnsJson.Trim();
        return raw.Length > 120 ? raw[..120] + "..." : raw;
    }

    private sealed class AuditReportFilterLabels
    {
        public string EntityName { get; set; } = "(todas)";
        public string RecordId { get; set; } = "(todos)";
        public string Action { get; set; } = "(todas)";
        public string Email { get; set; } = "(todos)";
        public string Periodo { get; set; } = "(sin límite)";
        public string Search { get; set; } = "(vacío)";
    }

    private sealed class AuditExportRow
    {
        public long Id { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public int? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserRole { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string RecordId { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? OldValuesJson { get; set; }
        public string? NewValuesJson { get; set; }
        public string? ChangedColumnsJson { get; set; }
        public string ChangedColumnsSummary { get; set; } = "Sin detalle";
    }
}