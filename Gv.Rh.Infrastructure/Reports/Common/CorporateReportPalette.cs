namespace Gv.Rh.Infrastructure.Reports.Common;

public static class CorporateReportPalette
{
    // Marca / acentos principales
    public const string BrandPrimary = "#1D4ED8";
    public const string BrandPrimaryDark = "#1E3A8A";
    public const string BrandPrimarySoft = "#DBEAFE";

    public const string BrandTeal = "#0F766E";
    public const string BrandPurple = "#7C3AED";
    public const string BrandAmber = "#B45309";

    // Superficies / texto / bordes
    public const string White = "#FFFFFF";
    public const string Surface = "#FFFFFF";
    public const string SurfaceAlt = "#F8FAFC";
    public const string SurfaceMuted = "#FAFCFF";

    public const string Ink900 = "#0F172A";
    public const string Ink700 = "#334155";
    public const string Ink600 = "#475569";
    public const string Ink500 = "#64748B";
    public const string Ink400 = "#94A3B8";

    public const string Border = "#D7DFEA";
    public const string BorderSoft = "#E5EAF2";
    public const string Divider = "#DCE4F0";

    // Estados
    public const string Success = "#15803D";
    public const string SuccessSoft = "#DCFCE7";

    public const string Warning = "#B45309";
    public const string WarningSoft = "#FEF3C7";

    public const string Danger = "#B91C1C";
    public const string DangerSoft = "#FEE2E2";

    public const string Info = "#1D4ED8";
    public const string InfoSoft = "#DBEAFE";

    public const string Neutral = "#475569";
    public const string NeutralSoft = "#E2E8F0";

    // Tabla
    public const string TableHeaderBackground = "#214FC6";
    public const string TableHeaderText = White;
    public const string TableRowOdd = White;
    public const string TableRowEven = SurfaceMuted;

    // KPIs / cards sugeridas
    public const string KpiPrimary = BrandPrimary;
    public const string KpiSuccess = Success;
    public const string KpiWarning = Warning;
    public const string KpiPurple = BrandPurple;
    public const string KpiTeal = BrandTeal;

    public static string GetAlternatingRowBackground(int index)
        => index % 2 == 0 ? TableRowOdd : TableRowEven;

    public static string GetBooleanAccent(bool value)
        => value ? Success : Warning;

    public static string GetIncidenciaStatusAccent(string? value)
        => NormalizeKey(value) switch
        {
            "PENDIENTE" => Warning,
            "APROBADA" => Success,
            "RECHAZADA" => Danger,
            _ => Info
        };

    public static string GetEmpleadoStatusAccent(string? value)
        => NormalizeKey(value) switch
        {
            "ACTIVO" => Success,
            "BAJA" => Warning,
            "REINGRESO" => Info,
            _ => Neutral
        };

    public static string GetDocumentoStatusAccent(string? value)
        => NormalizeKey(value) switch
        {
            "VIGENTE" => Success,
            "CARGADO" => Info,
            "PENDIENTE" => Warning,
            "PORVENCER" => BrandPurple,
            "VENCIDO" => Danger,
            _ => Neutral
        };

    public static string GetAuditActionAccent(string? value)
        => NormalizeKey(value) switch
        {
            "LOGIN" => Info,
            "REFRESH" => BrandTeal,
            "LOGOUT" => Neutral,
            "LOGOUTALL" => Warning,
            "CREATE" => Success,
            "UPDATE" => BrandPrimary,
            "SOFTDELETE" => Warning,
            "DELETE" => Danger,
            "RESTORE" => Success,
            _ => Neutral
        };

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToUpperInvariant();
    }
}