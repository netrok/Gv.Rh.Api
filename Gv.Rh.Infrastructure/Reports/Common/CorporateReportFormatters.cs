using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Gv.Rh.Infrastructure.Reports.Common;

public static class CorporateReportFormatters
{
    private static readonly CultureInfo ReportCulture = CultureInfo.InvariantCulture;

    public static string NullSafe(string? value, string fallback = "—")
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    public static string FormatLabel(string? value, string fallback = "—")
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var normalized = value
            .Trim()
            .Replace("_", " ")
            .Replace("-", " ");

        return string.Join(
            " ",
            normalized
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word =>
                {
                    var lower = word.ToLowerInvariant();
                    return char.ToUpperInvariant(lower[0]) + lower[1..];
                }));
    }

    public static string FormatUpperKeyLabel(string? value, string fallback = "—")
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim()
            .Replace("_", " ")
            .Replace("-", " ")
            .ToUpperInvariant();
    }

    public static string FormatBool(bool value, string trueLabel = "Sí", string falseLabel = "No")
    {
        return value ? trueLabel : falseLabel;
    }

    public static string FormatBoolNullable(bool? value, string fallback = "—")
    {
        return value.HasValue ? FormatBool(value.Value) : fallback;
    }

    public static string FormatDate(DateOnly value, string format = "dd/MM/yyyy")
    {
        return value.ToString(format, ReportCulture);
    }

    public static string FormatDateNullable(DateOnly? value, string format = "dd/MM/yyyy", string fallback = "—")
    {
        return value.HasValue
            ? value.Value.ToString(format, ReportCulture)
            : fallback;
    }

    public static string FormatDateTime(DateTime value, string format = "yyyy-MM-dd HH:mm:ss")
    {
        return value.ToString(format, ReportCulture);
    }

    public static string FormatDateTimeNullable(DateTime? value, string format = "yyyy-MM-dd HH:mm:ss", string fallback = "—")
    {
        return value.HasValue
            ? value.Value.ToString(format, ReportCulture)
            : fallback;
    }

    public static string FormatUtcStamp(DateTime value)
    {
        return $"{value.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC";
    }

    public static string FormatDateRange(
        DateOnly? from,
        DateOnly? to,
        string emptyFallback = "(sin límite)")
    {
        if (!from.HasValue && !to.HasValue)
            return emptyFallback;

        if (from.HasValue && to.HasValue)
            return $"{FormatDate(from.Value)} al {FormatDate(to.Value)}";

        if (from.HasValue)
            return $"Desde {FormatDate(from.Value)}";

        return $"Hasta {FormatDate(to!.Value)}";
    }

    public static string FormatNumericId(int? value, string fallbackAll = "(todos)")
    {
        return value.HasValue
            ? value.Value.ToString(ReportCulture)
            : fallbackAll;
    }

    public static string FormatSearchTerm(string? value, string fallback = "(vacío)")
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    public static string ExcelText(string? value, string fallback = "—")
    {
        return NullSafe(value, fallback);
    }

    public static string ExcelDate(DateOnly value, string format = "dd/MM/yyyy")
    {
        return value.ToString(format, ReportCulture);
    }

    public static string ExcelDate(DateOnly? value, string fallback = "—", string format = "dd/MM/yyyy")
    {
        return value.HasValue
            ? value.Value.ToString(format, ReportCulture)
            : fallback;
    }

    public static string ExcelDateTime(DateTime value, string format = "yyyy-MM-dd HH:mm:ss")
    {
        return value.ToString(format, ReportCulture);
    }

    public static string ExcelDateTime(DateTime? value, string fallback = "—", string format = "yyyy-MM-dd HH:mm:ss")
    {
        return value.HasValue
            ? value.Value.ToString(format, ReportCulture)
            : fallback;
    }

    public static string NormalizeFileSegment(string? value, string fallback = "sin_valor")
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var normalized = value.Trim().ToLowerInvariant();
        normalized = RemoveDiacritics(normalized);
        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", "_");
        normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');

        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    public static string BuildTimestampedFileName(
        string prefix,
        string extension,
        params string?[] segments)
    {
        var safePrefix = NormalizeFileSegment(prefix, "reporte");
        var safeExtension = extension.Trim().TrimStart('.');

        var parts = new List<string>
        {
            safePrefix,
            DateTime.Now.ToString("yyyyMMdd_HHmmss", ReportCulture)
        };

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
                continue;

            parts.Add(NormalizeFileSegment(segment));
        }

        return string.Join("_", parts) + "." + safeExtension;
    }

    public static string CombineFullName(params string?[] parts)
    {
        return string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
    }

    public static string FormatNullableLabel(string? value, string fallback = "—")
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : FormatLabel(value, fallback);
    }

    public static string FormatTipoDocumento(string? value, string fallback = "—")
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim() switch
        {
            "Ine" => "INE",
            "Curp" => "CURP",
            "Rfc" => "RFC",
            "Nss" => "NSS",
            "ActaNacimiento" => "Acta de nacimiento",
            "ComprobanteDomicilio" => "Comprobante de domicilio",
            "Contrato" => "Contrato",
            "CartaPolicia" => "Carta policía",
            _ => FormatLabel(value, fallback)
        };
    }

    public static string FormatFilterValue(string? value, string fallback = "(todos)")
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(capacity: normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}