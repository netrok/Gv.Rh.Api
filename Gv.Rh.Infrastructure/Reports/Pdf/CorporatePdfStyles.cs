using Gv.Rh.Infrastructure.Reports.Common;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Gv.Rh.Infrastructure.Reports.Pdf;

public static class CorporatePdfStyles
{
    public static IContainer ReportCard(
        this IContainer container,
        string backgroundColor = CorporateReportPalette.Surface,
        string borderColor = CorporateReportPalette.Border,
        float cornerRadius = 8,
        float padding = 10)
    {
        return container
            .Background(backgroundColor)
            .Border(1)
            .BorderColor(borderColor)
            .CornerRadius(cornerRadius)
            .Padding(padding);
    }

    public static IContainer ReportMutedCard(
        this IContainer container,
        float cornerRadius = 8,
        float padding = 10)
    {
        return container.ReportCard(
            backgroundColor: CorporateReportPalette.SurfaceAlt,
            borderColor: CorporateReportPalette.Border,
            cornerRadius: cornerRadius,
            padding: padding);
    }

    public static IContainer TableHeaderCell(this IContainer container)
    {
        return container
            .Background(CorporateReportPalette.TableHeaderBackground)
            .Border(1)
            .BorderColor(CorporateReportPalette.TableHeaderBackground)
            .PaddingVertical(7)
            .PaddingHorizontal(6)
            .AlignCenter()
            .AlignMiddle()
            .DefaultTextStyle(x => x
                .FontSize(8.1f)
                .FontColor(CorporateReportPalette.TableHeaderText)
                .SemiBold());
    }

    public static IContainer TableDataCell(
        this IContainer container,
        string? background = null,
        bool emphasize = false)
    {
        return container
            .Background(background ?? CorporateReportPalette.TableRowOdd)
            .BorderBottom(1)
            .BorderLeft(1)
            .BorderRight(1)
            .BorderColor(CorporateReportPalette.Divider)
            .PaddingVertical(6)
            .PaddingHorizontal(6)
            .DefaultTextStyle(x =>
            {
                var style = x
                    .FontSize(8.6f)
                    .FontColor(CorporateReportPalette.Ink900);

                return emphasize ? style.SemiBold() : style;
            });
    }

    public static IContainer EmptyState(this IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(CorporateReportPalette.Border)
            .CornerRadius(6)
            .Background(CorporateReportPalette.White)
            .Padding(10)
            .AlignCenter()
            .AlignMiddle()
            .DefaultTextStyle(x => x
                .FontSize(9)
                .FontColor(CorporateReportPalette.Ink500));
    }

    public static void AccentBar(
        this IContainer container,
        string accentColor,
        float thickness = 3)
    {
        container.LineHorizontal(thickness).LineColor(accentColor);
    }

    public static IContainer SectionTitleBlock(this IContainer container)
    {
        return container
            .PaddingBottom(2)
            .DefaultTextStyle(x => x
                .FontSize(11)
                .Bold()
                .FontColor(CorporateReportPalette.Ink900));
    }

    public static IContainer LabelText(this IContainer container)
    {
        return container.DefaultTextStyle(x => x
            .FontSize(7.8f)
            .SemiBold()
            .FontColor(CorporateReportPalette.Ink500));
    }

    public static IContainer ValueText(
        this IContainer container,
        bool emphasize = false,
        string color = CorporateReportPalette.Ink900)
    {
        return container.DefaultTextStyle(x =>
        {
            var style = x
                .FontSize(10)
                .FontColor(color);

            return emphasize ? style.SemiBold() : style;
        });
    }

    public static IContainer Chip(
        this IContainer container,
        string accentColor,
        string borderColor = CorporateReportPalette.Border)
    {
        return container
            .Background(CorporateReportPalette.White)
            .Border(1)
            .BorderColor(borderColor)
            .CornerRadius(7)
            .PaddingVertical(5)
            .PaddingHorizontal(8)
            .DefaultTextStyle(x => x.FontColor(accentColor));
    }

    public static IContainer KpiCard(
        this IContainer container,
        string borderColor = CorporateReportPalette.Border)
    {
        return container
            .Background(CorporateReportPalette.White)
            .Border(1)
            .BorderColor(borderColor)
            .CornerRadius(8)
            .Padding(11);
    }

    public static IContainer FooterText(this IContainer container)
    {
        return container.DefaultTextStyle(x => x
            .FontSize(8)
            .FontColor(CorporateReportPalette.Ink500));
    }

    public static string ZebraRow(int index)
        => CorporateReportPalette.GetAlternatingRowBackground(index);
}