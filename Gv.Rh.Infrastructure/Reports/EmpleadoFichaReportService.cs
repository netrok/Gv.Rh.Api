using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Domain.Common;
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

    private static readonly TipoDocumentoEmpleado[] RequiredTipos =
    [
        TipoDocumentoEmpleado.Ine,
        TipoDocumentoEmpleado.Curp,
        TipoDocumentoEmpleado.Rfc,
        TipoDocumentoEmpleado.Nss,
        TipoDocumentoEmpleado.ActaNacimiento,
        TipoDocumentoEmpleado.ComprobanteDomicilio,
        TipoDocumentoEmpleado.Contrato,
        TipoDocumentoEmpleado.CartaPolicia
    ];

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

        var movimientos = await _context.EmpleadoMovimientosLaborales
            .AsNoTracking()
            .Where(x => x.EmpleadoId == empleadoId)
            .OrderByDescending(x => x.FechaMovimiento)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .Select(x => new EmpleadoFichaMovimientoViewModel
            {
                TipoMovimiento = x.TipoMovimiento.ToString(),
                FechaMovimiento = x.FechaMovimiento,
                TipoBaja = x.TipoBaja != null ? x.TipoBaja.ToString() : null,
                Motivo = x.Motivo,
                Comentario = x.Comentario
            })
            .ToListAsync(cancellationToken);

        var documentos = await _context.EmpleadoDocumentos
            .AsNoTracking()
            .Where(x => x.EmpleadoId == empleadoId && x.Activo)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var latestByTipo = documentos
            .GroupBy(x => x.Tipo)
            .ToDictionary(g => g.Key, g => g.First());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var warningDate = today.AddDays(30);

        var documentoItems = new List<EmpleadoFichaDocumentoViewModel>();
        var cargados = 0;
        var faltantes = 0;
        var porVencer = 0;
        var vencidos = 0;

        foreach (var tipo in RequiredTipos)
        {
            latestByTipo.TryGetValue(tipo, out var documento);

            if (documento is null)
            {
                faltantes++;
                documentoItems.Add(new EmpleadoFichaDocumentoViewModel
                {
                    Tipo = tipo.ToString(),
                    Estado = "Pendiente",
                    FechaVencimiento = null
                });
                continue;
            }

            if (!documento.FechaVencimiento.HasValue)
            {
                cargados++;
                documentoItems.Add(new EmpleadoFichaDocumentoViewModel
                {
                    Tipo = tipo.ToString(),
                    Estado = "Cargado",
                    FechaVencimiento = null
                });
                continue;
            }

            var fechaVencimiento = documento.FechaVencimiento.Value;

            if (fechaVencimiento < today)
            {
                vencidos++;
                documentoItems.Add(new EmpleadoFichaDocumentoViewModel
                {
                    Tipo = tipo.ToString(),
                    Estado = "Vencido",
                    FechaVencimiento = fechaVencimiento
                });
                continue;
            }

            if (fechaVencimiento <= warningDate)
            {
                cargados++;
                porVencer++;
                documentoItems.Add(new EmpleadoFichaDocumentoViewModel
                {
                    Tipo = tipo.ToString(),
                    Estado = "Por vencer",
                    FechaVencimiento = fechaVencimiento
                });
                continue;
            }

            cargados++;
            documentoItems.Add(new EmpleadoFichaDocumentoViewModel
            {
                Tipo = tipo.ToString(),
                Estado = "Vigente",
                FechaVencimiento = fechaVencimiento
            });
        }

        empleado.Movimientos = movimientos;
        empleado.Documentos = documentoItems;
        empleado.DocumentosCargados = cargados;
        empleado.DocumentosFaltantes = faltantes;
        empleado.DocumentosPorVencer = porVencer;
        empleado.DocumentosVencidos = vencidos;

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
                    column.Item().Element(container => ComposeSituacionLaboral(container, empleado));
                    column.Item().Element(container => ComposeHistorialLaboral(container, empleado.Movimientos));
                    column.Item().Element(container => ComposeSituacionDocumental(container, empleado));
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
                "Puesto",
                NullSafe(empleado.Puesto),
                "Posición vigente",
                "#B45309");

            SummaryCard(
                row.RelativeItem(),
                "Departamento",
                NullSafe(empleado.Departamento),
                "Área organizacional",
                "#1D4ED8");

            SummaryCard(
                row.RelativeItem(),
                "Sucursal",
                NullSafe(empleado.Sucursal),
                "Centro de trabajo actual",
                "#7C3AED");

            SummaryCard(
                row.RelativeItem(),
                "Ingreso",
                FormatDate(empleado.FechaIngreso),
                "Fecha de alta",
                "#0F766E");
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
                    Field(left, "Fecha de nacimiento", FormatDateNullable(empleado.FechaNacimiento));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(6);
                    Field(right, "Teléfono", NullSafe(empleado.Telefono));
                    Field(right, "Correo", NullSafe(empleado.Email));
                });
            });
        });
    }

    private static void ComposeSituacionLaboral(IContainer container, EmpleadoFichaViewModel empleado)
    {
        SectionCard(container, "Situación laboral", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(18);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(6);
                    Field(left, "Fecha de baja actual", FormatDateNullable(empleado.FechaBajaActual));
                    Field(left, "Tipo de baja actual", FormatLabelNullable(empleado.TipoBajaActual));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(6);
                    Field(right, "Fecha de reingreso actual", FormatDateNullable(empleado.FechaReingresoActual));
                    Field(right, "Recontratable", FormatBoolNullable(empleado.Recontratable));
                });
            });
        });
    }

    private static void ComposeHistorialLaboral(
        IContainer container,
        IReadOnlyList<EmpleadoFichaMovimientoViewModel> movimientos)
    {
        SectionCard(container, "Historial laboral", body =>
        {
            if (movimientos.Count == 0)
            {
                body.Item()
                    .Border(1)
                    .BorderColor("#E5EAF2")
                    .CornerRadius(6)
                    .PaddingVertical(6)
                    .PaddingHorizontal(8)
                    .Text("Sin movimientos laborales registrados.")
                    .FontSize(8.5f)
                    .FontColor("#64748B");

                return;
            }

            body.Item().Column(list =>
            {
                list.Spacing(4);

                foreach (var movimiento in movimientos)
                {
                    list.Item()
                        .Border(1)
                        .BorderColor("#E5EAF2")
                        .CornerRadius(6)
                        .PaddingVertical(6)
                        .PaddingHorizontal(8)
                        .Column(card =>
                        {
                            card.Spacing(3);

                            card.Item().Row(row =>
                            {
                                row.RelativeItem().Text(FormatLabel(movimiento.TipoMovimiento))
                                    .FontSize(9.2f)
                                    .SemiBold()
                                    .FontColor("#0F172A");

                                row.ConstantItem(82).AlignRight().Text(FormatDate(movimiento.FechaMovimiento))
                                    .FontSize(8.1f)
                                    .FontColor("#64748B");
                            });

                            card.Item().Row(row =>
                            {
                                row.Spacing(10);

                                row.RelativeItem().Text(text =>
                                {
                                    text.Span("Tipo baja: ").SemiBold().FontColor("#64748B");
                                    text.Span(FormatLabelNullable(movimiento.TipoBaja)).FontColor("#0F172A");
                                });

                                row.RelativeItem().Text(text =>
                                {
                                    text.Span("Motivo: ").SemiBold().FontColor("#64748B");
                                    text.Span(NullSafe(movimiento.Motivo)).FontColor("#0F172A");
                                });
                            });

                            if (!string.IsNullOrWhiteSpace(movimiento.Comentario))
                            {
                                card.Item().Text(text =>
                                {
                                    text.Span("Comentario: ").SemiBold().FontColor("#64748B");
                                    text.Span(movimiento.Comentario!.Trim()).FontColor("#0F172A");
                                });
                            }
                        });
                }
            });
        });
    }

    private static void ComposeSituacionDocumental(
        IContainer container,
        EmpleadoFichaViewModel empleado)
    {
        SectionCard(container, "Situación documental", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(6);

                MiniStatCard(row.RelativeItem(), "Requeridos", RequiredTipos.Length.ToString(CultureInfo.InvariantCulture), "#1D4ED8");
                MiniStatCard(row.RelativeItem(), "Cargados", empleado.DocumentosCargados.ToString(CultureInfo.InvariantCulture), "#15803D");
                MiniStatCard(row.RelativeItem(), "Pendientes", empleado.DocumentosFaltantes.ToString(CultureInfo.InvariantCulture), "#B45309");
                MiniStatCard(row.RelativeItem(), "Por vencer", empleado.DocumentosPorVencer.ToString(CultureInfo.InvariantCulture), "#7C3AED");
                MiniStatCard(row.RelativeItem(), "Vencidos", empleado.DocumentosVencidos.ToString(CultureInfo.InvariantCulture), "#B91C1C");
            });

            body.Item().PaddingTop(4).Column(list =>
            {
                list.Spacing(4);

                foreach (var documento in empleado.Documentos)
                {
                    list.Item()
                        .Border(1)
                        .BorderColor("#E5EAF2")
                        .CornerRadius(6)
                        .PaddingVertical(5)
                        .PaddingHorizontal(8)
                        .Row(row =>
                        {
                            row.RelativeItem().Text(FormatTipoDocumento(documento.Tipo))
                                .FontSize(8.8f)
                                .SemiBold()
                                .FontColor("#0F172A");

                            row.ConstantItem(88).AlignCenter().Text(documento.Estado)
                                .FontSize(8f)
                                .SemiBold()
                                .FontColor(GetEstadoDocumentoColor(documento.Estado));

                            row.ConstantItem(88).AlignRight().Text(
                                documento.FechaVencimiento.HasValue
                                    ? FormatDate(documento.FechaVencimiento.Value)
                                    : "—")
                                .FontSize(8f)
                                .FontColor("#64748B");
                        });
                }
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
            .Padding(10)
            .Column(column =>
            {
                column.Spacing(6);

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

    private static void MiniStatCard(IContainer container, string title, string value, string color)
    {
        container
            .Border(1)
            .BorderColor("#E5EAF2")
            .CornerRadius(6)
            .PaddingVertical(6)
            .PaddingHorizontal(8)
            .Column(column =>
            {
                column.Spacing(1);

                column.Item().Text(title)
                    .FontSize(7.1f)
                    .FontColor("#64748B");

                column.Item().Text(value)
                    .FontSize(10f)
                    .SemiBold()
                    .FontColor(color);
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

    private static string FormatTipoDocumento(string? value)
    {
        return value switch
        {
            "Ine" => "INE",
            "Curp" => "CURP",
            "Rfc" => "RFC",
            "Nss" => "NSS",
            "ActaNacimiento" => "Acta de nacimiento",
            "ComprobanteDomicilio" => "Comprobante de domicilio",
            "Contrato" => "Contrato",
            "CartaPolicia" => "Carta policía",
            _ => FormatLabel(value)
        };
    }

    private static string GetEstadoDocumentoColor(string estado)
    {
        return estado switch
        {
            "Vigente" => "#15803D",
            "Cargado" => "#1D4ED8",
            "Por vencer" => "#7C3AED",
            "Vencido" => "#B91C1C",
            "Pendiente" => "#B45309",
            _ => "#475569"
        };
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

        public List<EmpleadoFichaMovimientoViewModel> Movimientos { get; set; } = [];
        public List<EmpleadoFichaDocumentoViewModel> Documentos { get; set; } = [];
        public int DocumentosCargados { get; set; }
        public int DocumentosFaltantes { get; set; }
        public int DocumentosPorVencer { get; set; }
        public int DocumentosVencidos { get; set; }
    }

    private sealed class EmpleadoFichaMovimientoViewModel
    {
        public string TipoMovimiento { get; set; } = string.Empty;
        public DateOnly FechaMovimiento { get; set; }
        public string? TipoBaja { get; set; }
        public string? Motivo { get; set; }
        public string? Comentario { get; set; }
    }

    private sealed class EmpleadoFichaDocumentoViewModel
    {
        public string Tipo { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateOnly? FechaVencimiento { get; set; }
    }
}