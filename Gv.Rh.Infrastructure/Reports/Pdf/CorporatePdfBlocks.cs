using Gv.Rh.Infrastructure.Reports.Common;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Gv.Rh.Infrastructure.Reports.Pdf;

public static class CorporatePdfBlocks
{
    public static void ComposeReportHeader(
        IContainer container,
        string title,
        string? subtitle,
        DateTime generatedAtUtc,
        string systemName = "GV RH · Recursos Humanos",
        string? rightBadgeText = "Reporte corporativo")
    {
        container.PaddingBottom(8).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.Spacing(12);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(2);

                    left.Item().Text(systemName)
                        .FontSize(10)
                        .FontColor(CorporateReportPalette.Ink600)
                        .SemiBold();

                    left.Item().Text(string.IsNullOrWhiteSpace(title) ? "Reporte" : title.Trim())
                        .FontSize(20)
                        .Bold()
                        .FontColor(CorporateReportPalette.Ink900);

                    if (!string.IsNullOrWhiteSpace(subtitle))
                    {
                        left.Item().Text(subtitle!.Trim())
                            .FontSize(10.5f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.BrandPrimary);
                    }

                    left.Item().Text($"Generado: {CorporateReportFormatters.FormatUtcStamp(generatedAtUtc)}")
                        .FontSize(8.5f)
                        .FontColor(CorporateReportPalette.Ink500);
                });

                if (!string.IsNullOrWhiteSpace(rightBadgeText))
                {
                    row.ConstantItem(180).AlignRight().AlignTop().Element(badge =>
                    {
                        badge.Chip(CorporateReportPalette.BrandPrimary).Text(rightBadgeText!.Trim())
                            .FontSize(8.8f)
                            .SemiBold()
                            .FontColor(CorporateReportPalette.BrandPrimary)
                            .AlignCenter();
                    });
                }
            });

            column.Item()
                .PaddingTop(6)
                .LineHorizontal(1)
                .LineColor(CorporateReportPalette.Border);
        });
    }

    public static void ComposeReportFooter(
        IContainer container,
        DateTime generatedAtUtc,
        string? leftText = null)
    {
        container.PaddingTop(6).Column(column =>
        {
            column.Item()
                .LineHorizontal(1)
                .LineColor(CorporateReportPalette.Border);

            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(
                        string.IsNullOrWhiteSpace(leftText)
                            ? $"Emitido: {CorporateReportFormatters.FormatUtcStamp(generatedAtUtc)}"
                            : leftText!.Trim())
                    .FontSize(8)
                    .FontColor(CorporateReportPalette.Ink500);

                row.ConstantItem(90).AlignRight().Text(text =>
                {
                    text.Span("Página ").FontSize(8).FontColor(CorporateReportPalette.Ink500);
                    text.CurrentPageNumber().FontSize(8).SemiBold();
                    text.Span(" de ").FontSize(8).FontColor(CorporateReportPalette.Ink500);
                    text.TotalPages().FontSize(8).SemiBold();
                });
            });
        });
    }

    public static void ComposeFiltersPanel(
        IContainer container,
        string title,
        IEnumerable<(string Label, string Value)> filters)
    {
        var filterList = filters
            .Where(x => !string.IsNullOrWhiteSpace(x.Label))
            .Select(x => (
                Label: x.Label.Trim(),
                Value: string.IsNullOrWhiteSpace(x.Value) ? "No registrado" : x.Value.Trim()))
            .ToList();

        container.ReportMutedCard().Column(column =>
        {
            column.Spacing(8);

            column.Item().Text(string.IsNullOrWhiteSpace(title) ? "Filtros aplicados" : title.Trim())
                .FontSize(11)
                .Bold()
                .FontColor(CorporateReportPalette.Ink900);

            if (filterList.Count == 0)
            {
                column.Item().Text("Sin filtros aplicados.")
                    .FontSize(9)
                    .FontColor(CorporateReportPalette.Ink500);
                return;
            }

            var leftItems = filterList.Where((_, index) => index % 2 == 0).ToList();
            var rightItems = filterList.Where((_, index) => index % 2 == 1).ToList();

            column.Item().Row(row =>
            {
                row.Spacing(18);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);
                    foreach (var item in leftItems)
                        AddFilterText(left, item.Label, item.Value);
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);
                    foreach (var item in rightItems)
                        AddFilterText(right, item.Label, item.Value);
                });
            });
        });
    }

    public static void ComposeKpiRow(
        IContainer container,
        params (string Title, string Value, string Subtitle, string AccentColor)[] items)
    {
        var validItems = items
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .ToList();

        if (validItems.Count == 0)
            return;

        container.Row(row =>
        {
            row.Spacing(10);

            foreach (var item in validItems)
            {
                row.RelativeItem().Element(card =>
                    ComposeKpiCard(
                        card,
                        item.Title,
                        item.Value,
                        item.Subtitle,
                        string.IsNullOrWhiteSpace(item.AccentColor)
                            ? CorporateReportPalette.BrandPrimary
                            : item.AccentColor));
            }
        });
    }

    public static void ComposeSection(
        IContainer container,
        string title,
        Action<ColumnDescriptor> buildBody)
    {
        container.ReportCard().Column(column =>
        {
            column.Spacing(6);

            column.Item().Text(string.IsNullOrWhiteSpace(title) ? "Sección" : title.Trim())
                .FontSize(11)
                .Bold()
                .FontColor(CorporateReportPalette.Ink900);

            column.Item()
                .LineHorizontal(1)
                .LineColor(CorporateReportPalette.BorderSoft);

            column.Item().Column(body =>
            {
                body.Spacing(4);
                buildBody(body);
            });
        });
    }

    public static void ComposeLabeledField(
        ColumnDescriptor column,
        string label,
        string value)
    {
        column.Item().PaddingBottom(2).Column(item =>
        {
            item.Spacing(1.5f);

            item.Item().Text(string.IsNullOrWhiteSpace(label) ? "Campo" : label.Trim())
                .FontSize(7.7f)
                .SemiBold()
                .FontColor(CorporateReportPalette.Ink500);

            item.Item().Text(string.IsNullOrWhiteSpace(value) ? "No registrado" : value.Trim())
                .FontSize(10.1f)
                .SemiBold()
                .FontColor(CorporateReportPalette.Ink900);
        });
    }

    public static void ComposeStatusChip(
        IContainer container,
        string label,
        string value,
        string accentColor)
    {
        container.Chip(accentColor).Column(column =>
        {
            column.Spacing(1);

            column.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(label) ? "Estado" : label.Trim())
                .FontSize(7.5f)
                .FontColor(CorporateReportPalette.Ink500);

            column.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(value) ? "No registrado" : value.Trim())
                .FontSize(9)
                .SemiBold()
                .FontColor(accentColor);
        });
    }

    public static void ComposeEmptyState(
        IContainer container,
        string message)
    {
        container.EmptyState()
            .Text(string.IsNullOrWhiteSpace(message) ? "Sin información disponible." : message.Trim());
    }

    private static void AddFilterText(ColumnDescriptor column, string label, string value)
    {
        column.Item().Text(text =>
        {
            text.Span($"{label}: ").SemiBold().FontColor(CorporateReportPalette.Ink700);
            text.Span(value).FontColor(CorporateReportPalette.Ink900);
        });
    }

    private static void ComposeKpiCard(
        IContainer container,
        string title,
        string value,
        string subtitle,
        string accentColor)
    {
        container.KpiCard().Column(column =>
        {
            column.Spacing(4);

            column.Item().AccentBar(accentColor);

            column.Item().PaddingTop(4).Text(title.Trim())
                .FontSize(10)
                .SemiBold()
                .FontColor(CorporateReportPalette.Ink600);

            column.Item().Text(string.IsNullOrWhiteSpace(value) ? "No registrado" : value.Trim())
                .FontSize(18.5f)
                .Bold()
                .FontColor(accentColor);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                column.Item().Text(subtitle.Trim())
                    .FontSize(8.5f)
                    .FontColor(CorporateReportPalette.Ink500);
            }
        });
    }
}