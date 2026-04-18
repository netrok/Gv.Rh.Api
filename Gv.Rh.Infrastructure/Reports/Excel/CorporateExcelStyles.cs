using ClosedXML.Excel;
using Gv.Rh.Infrastructure.Reports.Common;
using System.Globalization;

namespace Gv.Rh.Infrastructure.Reports.Excel;

public static class CorporateExcelStyles
{
    private static readonly CultureInfo ReportCulture = CultureInfo.InvariantCulture;

    public static void ApplyWorkbookDefaults(XLWorkbook workbook)
    {
        workbook.Style.Font.FontName = "Aptos";
        workbook.Style.Font.FontSize = 10;
        workbook.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        workbook.Style.Alignment.WrapText = false;
    }

    public static void WriteReportHeader(
        IXLWorksheet ws,
        string title,
        string? subtitle,
        DateTime generatedAtUtc,
        int startColumn,
        int endColumn,
        int startRow = 1)
    {
        var titleRange = ws.Range(startRow, startColumn, startRow, endColumn);
        titleRange.Merge();
        titleRange.Value = title;

        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 16;
        titleRange.Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.Ink900);
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var subtitleRange = ws.Range(startRow + 1, startColumn, startRow + 1, endColumn);
            subtitleRange.Merge();
            subtitleRange.Value = subtitle!.Trim();

            subtitleRange.Style.Font.Bold = true;
            subtitleRange.Style.Font.FontSize = 10;
            subtitleRange.Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.BrandPrimary);
            subtitleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        ws.Cell(startRow + 2, startColumn).Value = "Generado UTC";
        ws.Cell(startRow + 2, startColumn + 1).Value =
            CorporateReportFormatters.FormatDateTime(generatedAtUtc, "yyyy-MM-dd HH:mm:ss");

        ws.Cell(startRow + 2, startColumn).Style.Font.Bold = true;
        ws.Cell(startRow + 2, startColumn).Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.Ink600);
        ws.Cell(startRow + 2, startColumn + 1).Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.Ink900);
    }

    public static int WriteFiltersBlock(
        IXLWorksheet ws,
        int startRow,
        IEnumerable<(string Label, string Value)> filters,
        string title = "Filtros aplicados",
        int labelColumn = 1,
        int valueColumn = 2)
    {
        ws.Cell(startRow, labelColumn).Value = title;
        var titleRange = ws.Range(startRow, labelColumn, startRow, valueColumn);

        titleRange.Merge();
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.Ink900);
        titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml(CorporateReportPalette.SurfaceAlt);
        titleRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        titleRange.Style.Border.OutsideBorderColor = XLColor.FromHtml(CorporateReportPalette.Border);

        var row = startRow + 1;

        foreach (var (label, value) in filters.Where(x => !string.IsNullOrWhiteSpace(x.Label)))
        {
            ws.Cell(row, labelColumn).Value = label.Trim();
            ws.Cell(row, valueColumn).Value = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

            ws.Cell(row, labelColumn).Style.Font.Bold = true;
            ws.Cell(row, labelColumn).Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.Ink600);
            ws.Cell(row, valueColumn).Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.Ink900);

            row++;
        }

        var blockRange = ws.Range(startRow, labelColumn, Math.Max(startRow, row - 1), valueColumn);
        blockRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        blockRange.Style.Border.OutsideBorderColor = XLColor.FromHtml(CorporateReportPalette.Border);
        blockRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        blockRange.Style.Border.InsideBorderColor = XLColor.FromHtml(CorporateReportPalette.BorderSoft);

        ws.Column(labelColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ws.Column(valueColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        return row;
    }

    public static int WriteKpiCards(
        IXLWorksheet ws,
        int startRow,
        IReadOnlyList<(string Title, string Value, string AccentColor)> items,
        int startColumn = 1,
        int cardWidth = 2,
        int gap = 1)
    {
        if (items.Count == 0)
            return startRow;

        var currentColumn = startColumn;

        foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.Title)))
        {
            var fromCol = currentColumn;
            var toCol = currentColumn + cardWidth - 1;

            var accentRange = ws.Range(startRow, fromCol, startRow, toCol);
            accentRange.Merge();
            accentRange.Style.Fill.BackgroundColor = XLColor.FromHtml(
                string.IsNullOrWhiteSpace(item.AccentColor)
                    ? CorporateReportPalette.BrandPrimary
                    : item.AccentColor);

            var titleRange = ws.Range(startRow + 1, fromCol, startRow + 1, toCol);
            titleRange.Merge();
            titleRange.Value = item.Title.Trim();
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Font.FontSize = 10;
            titleRange.Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.Ink600);

            var valueRange = ws.Range(startRow + 2, fromCol, startRow + 2, toCol);
            valueRange.Merge();
            valueRange.Value = string.IsNullOrWhiteSpace(item.Value) ? "—" : item.Value.Trim();
            valueRange.Style.Font.Bold = true;
            valueRange.Style.Font.FontSize = 16;
            valueRange.Style.Font.FontColor = XLColor.FromHtml(
                string.IsNullOrWhiteSpace(item.AccentColor)
                    ? CorporateReportPalette.BrandPrimary
                    : item.AccentColor);

            var cardRange = ws.Range(startRow, fromCol, startRow + 3, toCol);
            cardRange.Style.Fill.BackgroundColor = XLColor.FromHtml(CorporateReportPalette.White);
            cardRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cardRange.Style.Border.OutsideBorderColor = XLColor.FromHtml(CorporateReportPalette.Border);
            cardRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cardRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            currentColumn += cardWidth + gap;
        }

        return startRow + 4;
    }

    public static void ApplyTableHeader(IXLRange headerRange)
    {
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.TableHeaderText);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(CorporateReportPalette.TableHeaderBackground);
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.OutsideBorderColor = XLColor.FromHtml(CorporateReportPalette.TableHeaderBackground);
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorderColor = XLColor.FromHtml(CorporateReportPalette.TableHeaderBackground);
    }

    public static void ApplyDataGrid(
        IXLWorksheet ws,
        int firstDataRow,
        int lastDataRow,
        int firstColumn,
        int lastColumn,
        bool zebra = true)
    {
        if (lastDataRow < firstDataRow)
            return;

        for (var row = firstDataRow; row <= lastDataRow; row++)
        {
            var range = ws.Range(row, firstColumn, row, lastColumn);

            range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            range.Style.Border.BottomBorderColor = XLColor.FromHtml(CorporateReportPalette.Divider);
            range.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            range.Style.Border.LeftBorderColor = XLColor.FromHtml(CorporateReportPalette.Divider);
            range.Style.Border.RightBorder = XLBorderStyleValues.Thin;
            range.Style.Border.RightBorderColor = XLColor.FromHtml(CorporateReportPalette.Divider);

            if (zebra)
            {
                var background = CorporateReportPalette.GetAlternatingRowBackground(row - firstDataRow);
                range.Style.Fill.BackgroundColor = XLColor.FromHtml(background);
            }
        }
    }

    public static void FinalizeTable(
        IXLWorksheet ws,
        int headerRow,
        int firstColumn,
        int lastColumn,
        int lastDataRow,
        bool freezeHeaderRow = true,
        bool autoFilter = true)
    {
        var tableLastRow = Math.Max(headerRow + 1, lastDataRow);

        if (autoFilter)
            ws.Range(headerRow, firstColumn, tableLastRow, lastColumn).SetAutoFilter();

        if (freezeHeaderRow)
            ws.SheetView.FreezeRows(headerRow);

        ws.Range(headerRow, firstColumn, tableLastRow, lastColumn)
            .Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    public static void SetColumnWidths(IXLWorksheet ws, params double[] widths)
    {
        for (var i = 0; i < widths.Length; i++)
        {
            ws.Column(i + 1).Width = widths[i];
        }
    }

    public static void AutoFitUsedColumns(IXLWorksheet ws)
    {
        ws.ColumnsUsed().AdjustToContents();
    }

    public static void StyleSummaryLabel(IXLCell cell)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.Ink700);
    }

    public static void StyleSummaryValue(IXLCell cell, string? accentColor = null)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.FromHtml(
            string.IsNullOrWhiteSpace(accentColor)
                ? CorporateReportPalette.Ink900
                : accentColor);
    }

    public static void FormatDateColumn(
        IXLWorksheet ws,
        int columnNumber,
        int fromRow,
        int toRow,
        string format = "dd/MM/yyyy")
    {
        if (toRow < fromRow)
            return;

        ws.Range(fromRow, columnNumber, toRow, columnNumber)
            .Style.DateFormat.Format = format;
    }

    public static void FormatDateTimeColumn(
        IXLWorksheet ws,
        int columnNumber,
        int fromRow,
        int toRow,
        string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (toRow < fromRow)
            return;

        ws.Range(fromRow, columnNumber, toRow, columnNumber)
            .Style.DateFormat.Format = format;
    }

    public static void WriteSectionTitle(
        IXLWorksheet ws,
        int row,
        int column,
        string title)
    {
        ws.Cell(row, column).Value = title;
        ws.Cell(row, column).Style.Font.Bold = true;
        ws.Cell(row, column).Style.Font.FontColor = XLColor.FromHtml(CorporateReportPalette.Ink900);
    }

    public static string ExcelText(string? value, string fallback = "—")
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    public static string ExcelDate(DateOnly? value, string fallback = "—")
    {
        return value.HasValue
            ? value.Value.ToString("dd/MM/yyyy", ReportCulture)
            : fallback;
    }

    public static string ExcelDateTime(DateTime? value, string fallback = "—")
    {
        return value.HasValue
            ? value.Value.ToString("yyyy-MM-dd HH:mm:ss", ReportCulture)
            : fallback;
    }
}