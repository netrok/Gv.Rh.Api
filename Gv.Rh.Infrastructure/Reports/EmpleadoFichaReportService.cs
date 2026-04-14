using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace Gv.Rh.Infrastructure.Reports;

public sealed class EmpleadoFichaReportService : IEmpleadoFichaReportService
{
    private readonly RhDbContext _context;

    public EmpleadoFichaReportService(RhDbContext context)
    {
        _context = context;
    }

    public async Task<ReportFileDto> BuildPdfAsync(int empleadoId, CancellationToken cancellationToken)
    {
        var empleado = await _context.Empleados
            .AsNoTracking()
            .Include(x => x.Sucursal)
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Where(x => x.Id == empleadoId)
            .Select(x => new EmpleadoFichaViewModel
            {
                Id = x.Id,
                NumEmpleado = x.NumEmpleado,
                Nombres = x.Nombres,
                ApellidoPaterno = x.ApellidoPaterno,
                ApellidoMaterno = x.ApellidoMaterno,
                FechaNacimiento = x.FechaNacimiento,
                Telefono = x.Telefono,
                Email = x.Email,
                FechaIngreso = x.FechaIngreso,
                Activo = x.Activo,
                EstatusLaboralActual = x.EstatusLaboralActual.ToString(),
                FechaBajaActual = x.FechaBajaActual,
                TipoBajaActual = x.TipoBajaActual != null ? x.TipoBajaActual.ToString() : null,
                FechaReingresoActual = x.FechaReingresoActual,
                Recontratable = x.Recontratable,
                Sucursal = x.Sucursal != null ? x.Sucursal.Nombre : null,
                Departamento = x.Departamento != null ? x.Departamento.Nombre : null,
                Puesto = x.Puesto != null ? x.Puesto.Nombre : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (empleado is null)
            throw new InvalidOperationException("No se encontró el empleado solicitado.");

        var generatedAtUtc = DateTime.UtcNow;

        var pdfBytes = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#1F2937"));

                page.Header().Element(container => ComposeHeader(container, empleado, generatedAtUtc));

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(container => ComposeSummary(container, empleado));
                    column.Item().Element(container => ComposeDatosPersonales(container, empleado));
                    column.Item().Element(container => ComposeDatosLaborales(container, empleado));
                });

                page.Footer().Element(container => ComposeFooter(container, generatedAtUtc));
            });
        }).GeneratePdf();

        return new ReportFileDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = BuildFileName(empleado.NumEmpleado)
        };
    }

    private static void ComposeHeader(
        IContainer container,
        EmpleadoFichaViewModel empleado,
        DateTime generatedAtUtc)
    {
        container.PaddingBottom(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Spacing(3);

                    left.Item().Text("GV RH · Recursos Humanos")
                        .FontSize(10)
                        .FontColor("#475569")
                        .SemiBold();

                    left.Item().Text("Ficha de empleado")
                        .FontSize(21)
                        .Bold()
                        .FontColor("#0F172A");

                    left.Item().Text(GetNombreCompleto(empleado))
                        .FontSize(13.5f)
                        .SemiBold()
                        .FontColor("#1D4ED8");

                    left.Item().Text($"Generado UTC: {generatedAtUtc:yyyy-MM-dd HH:mm:ss}")
                        .FontSize(8.5f)
                        .FontColor("#64748B");
                });

                row.ConstantItem(215).AlignRight().Column(right =>
                {
                    right.Spacing(6);

                    right.Item().AlignRight().Text($"Empleado: {NullSafe(empleado.NumEmpleado)}")
                        .FontSize(10.5f)
                        .SemiBold()
                        .FontColor("#0F172A");

                    right.Item().AlignRight().Row(chips =>
                    {
                        chips.Spacing(6);

                        chips.RelativeItem().Element(c =>
                            StatusChip(
                                c,
                                "Estatus",
                                FormatLabel(empleado.EstatusLaboralActual),
                                "#1D4ED8"));

                        chips.RelativeItem().Element(c =>
                            StatusChip(
                                c,
                                "Activo",
                                empleado.Activo ? "Sí" : "No",
                                empleado.Activo ? "#15803D" : "#B45309"));
                    });
                });
            });

            column.Item().PaddingTop(7).LineHorizontal(1).LineColor("#D7DFEA");
        });
    }

    private static void ComposeSummary(IContainer container, EmpleadoFichaViewModel empleado)
    {
        container.Row(row =>
        {
            row.Spacing(10);

            SummaryCard(
                row.RelativeItem(),
                "Número de empleado",
                NullSafe(empleado.NumEmpleado),
                "Identificador interno",
                "#1D4ED8");

            SummaryCard(
                row.RelativeItem(),
                "Ingreso",
                FormatDate(empleado.FechaIngreso),
                "Fecha de alta",
                "#0F766E");

            SummaryCard(
                row.RelativeItem(),
                "Sucursal",
                NullSafe(empleado.Sucursal),
                "Centro de trabajo actual",
                "#7C3AED");

            SummaryCard(
                row.RelativeItem(),
                "Puesto",
                NullSafe(empleado.Puesto),
                "Posición vigente",
                "#B45309");
        });
    }

    private static void ComposeDatosPersonales(IContainer container, EmpleadoFichaViewModel empleado)
    {
        SectionCard(container, "Datos personales", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(18);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(6);
                    Field(left, "Nombres", NullSafe(empleado.Nombres));
                    Field(left, "Apellido paterno", NullSafe(empleado.ApellidoPaterno));
                    Field(left, "Apellido materno", NullSafe(empleado.ApellidoMaterno));
                    Field(left, "Nombre completo", GetNombreCompleto(empleado));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(6);
                    Field(right, "Fecha de nacimiento", FormatDateNullable(empleado.FechaNacimiento));
                    Field(right, "Teléfono", NullSafe(empleado.Telefono));
                    Field(right, "Correo", NullSafe(empleado.Email));
                });
            });
        });
    }

    private static void ComposeDatosLaborales(IContainer container, EmpleadoFichaViewModel empleado)
    {
        SectionCard(container, "Datos laborales", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(18);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(6);
                    Field(left, "Sucursal", NullSafe(empleado.Sucursal));
                    Field(left, "Departamento", NullSafe(empleado.Departamento));
                    Field(left, "Puesto", NullSafe(empleado.Puesto));
                    Field(left, "Fecha de ingreso", FormatDate(empleado.FechaIngreso));
                    Field(left, "Estatus laboral", FormatLabel(empleado.EstatusLaboralActual));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(6);
                    Field(right, "Activo", empleado.Activo ? "Sí" : "No");
                    Field(right, "Fecha de baja actual", FormatDateNullable(empleado.FechaBajaActual));
                    Field(right, "Tipo de baja actual", FormatLabelNullable(empleado.TipoBajaActual));
                    Field(right, "Fecha de reingreso actual", FormatDateNullable(empleado.FechaReingresoActual));
                    Field(right, "Recontratable", FormatBoolNullable(empleado.Recontratable));
                });
            });
        });
    }

    private static void SectionCard(IContainer container, string title, Action<ColumnDescriptor> buildBody)
    {
        container
            .Background("#FFFFFF")
            .Border(1)
            .BorderColor("#D7DFEA")
            .CornerRadius(8)
            .Padding(12)
            .Column(column =>
            {
                column.Spacing(8);

                column.Item().Text(title)
                    .FontSize(11)
                    .Bold()
                    .FontColor("#0F172A");

                column.Item().LineHorizontal(1).LineColor("#E5EAF2");

                column.Item().Column(body =>
                {
                    body.Spacing(4);
                    buildBody(body);
                });
            });
    }

    private static void Field(ColumnDescriptor column, string label, string value)
    {
        column.Item().PaddingBottom(2).Column(item =>
        {
            item.Spacing(1.5f);

            item.Item().Text(label)
                .FontSize(7.7f)
                .SemiBold()
                .FontColor("#64748B");

            item.Item().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                .FontSize(10.2f)
                .FontColor("#0F172A")
                .SemiBold();
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
            .Padding(11)
            .Column(column =>
            {
                column.Spacing(5);

                column.Item().LineHorizontal(3).LineColor(accentColor);

                column.Item().PaddingTop(3).Text(title)
                    .FontSize(8.5f)
                    .SemiBold()
                    .FontColor("#64748B");

                column.Item().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                    .FontSize(11.5f)
                    .Bold()
                    .FontColor("#0F172A");

                column.Item().Text(subtitle)
                    .FontSize(7.8f)
                    .FontColor("#94A3B8");
            });
    }

    private static void StatusChip(IContainer container, string label, string value, string accentColor)
    {
        container
            .Background("#FFFFFF")
            .Border(1)
            .BorderColor("#D7DFEA")
            .CornerRadius(7)
            .PaddingVertical(5)
            .PaddingHorizontal(8)
            .Column(column =>
            {
                column.Spacing(1);

                column.Item().AlignCenter().Text(label)
                    .FontSize(7.5f)
                    .FontColor("#64748B");

                column.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                    .FontSize(9)
                    .SemiBold()
                    .FontColor(accentColor);
            });
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

    private static string GetNombreCompleto(EmpleadoFichaViewModel empleado)
    {
        var parts = new[]
        {
            empleado.Nombres,
            empleado.ApellidoPaterno,
            empleado.ApellidoMaterno
        };

        return string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string NullSafe(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    private static string FormatDateNullable(DateOnly? value)
    {
        return value.HasValue
            ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : "—";
    }

    private static string FormatBoolNullable(bool? value)
    {
        return value.HasValue ? (value.Value ? "Sí" : "No") : "—";
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

    private static string FormatLabelNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : FormatLabel(value);
    }

    private static string BuildFileName(string numEmpleado)
    {
        var safeNumEmpleado = string.IsNullOrWhiteSpace(numEmpleado) ? "sin_numero" : numEmpleado.Trim();
        return $"ficha_empleado_{safeNumEmpleado}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
    }

    private sealed class EmpleadoFichaViewModel
    {
        public int Id { get; set; }
        public string NumEmpleado { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string? ApellidoMaterno { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Sucursal { get; set; }
        public string? Departamento { get; set; }
        public string? Puesto { get; set; }
        public DateOnly FechaIngreso { get; set; }
        public string EstatusLaboralActual { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public DateOnly? FechaBajaActual { get; set; }
        public string? TipoBajaActual { get; set; }
        public DateOnly? FechaReingresoActual { get; set; }
        public bool? Recontratable { get; set; }
    }
}