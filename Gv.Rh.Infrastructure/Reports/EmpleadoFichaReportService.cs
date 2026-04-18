using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Domain.Common;
using Gv.Rh.Infrastructure.Persistence;
using Gv.Rh.Infrastructure.Reports.Common;
using Gv.Rh.Infrastructure.Reports.Pdf;
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

        BuildDocumentStatus(empleado, documentos);

        empleado.Movimientos = movimientos;

        var generatedAtUtc = DateTime.UtcNow;
        var nombreCompleto = CorporateReportFormatters.CombineFullName(
            empleado.Nombres,
            empleado.ApellidoPaterno,
            empleado.ApellidoMaterno);

        var pdfBytes = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(CorporateReportPalette.Ink900));

                page.Header().Element(container =>
                    CorporatePdfBlocks.ComposeReportHeader(
                        container,
                        "Ficha de empleado",
                        nombreCompleto,
                        generatedAtUtc));

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Element(container => ComposeOverviewSection(container, empleado));
                    column.Item().Element(container => ComposeSummarySection(container, empleado));
                    column.Item().Element(container => ComposePersonalDataSection(container, empleado));
                    column.Item().Element(container => ComposeLaborSection(container, empleado));
                    column.Item().Element(container => ComposeHistorySection(container, empleado.Movimientos));
                    column.Item().Element(container => ComposeDocumentsSection(container, empleado));
                });

                page.Footer().Element(container =>
                    CorporatePdfBlocks.ComposeReportFooter(container, generatedAtUtc));
            });
        }).GeneratePdf();

        return new ReportFileDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = BuildFileName(empleado.NumEmpleado)
        };
    }

    private static void BuildDocumentStatus(
        EmpleadoFichaViewModel empleado,
        IReadOnlyList<Domain.Entities.EmpleadoDocumento> documentos)
    {
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

        empleado.Documentos = documentoItems;
        empleado.DocumentosCargados = cargados;
        empleado.DocumentosFaltantes = faltantes;
        empleado.DocumentosPorVencer = porVencer;
        empleado.DocumentosVencidos = vencidos;
    }

    private static void ComposeOverviewSection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeSection(container, "Identificación general", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(12);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(left, "Número de empleado", CorporateReportFormatters.NullSafe(empleado.NumEmpleado));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Nombre completo", CorporateReportFormatters.CombineFullName(
                        empleado.Nombres,
                        empleado.ApellidoPaterno,
                        empleado.ApellidoMaterno));
                });

                row.RelativeItem().Column(center =>
                {
                    center.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(center, "Puesto actual", CorporateReportFormatters.NullSafe(empleado.Puesto));
                    CorporatePdfBlocks.ComposeLabeledField(center, "Departamento", CorporateReportFormatters.NullSafe(empleado.Departamento));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(6);

                    right.Item().Row(chips =>
                    {
                        chips.Spacing(6);

                        chips.RelativeItem().Element(c =>
                            CorporatePdfBlocks.ComposeStatusChip(
                                c,
                                "Estatus",
                                CorporateReportFormatters.FormatLabel(empleado.EstatusLaboralActual),
                                CorporateReportPalette.GetEmpleadoStatusAccent(empleado.EstatusLaboralActual)));

                        chips.RelativeItem().Element(c =>
                            CorporatePdfBlocks.ComposeStatusChip(
                                c,
                                "Activo",
                                CorporateReportFormatters.FormatBool(empleado.Activo),
                                CorporateReportPalette.GetBooleanAccent(empleado.Activo)));
                    });

                    CorporatePdfBlocks.ComposeLabeledField(right, "Sucursal", CorporateReportFormatters.NullSafe(empleado.Sucursal));
                });
            });
        });
    }

    private static void ComposeSummarySection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeKpiRow(
            container,
            ("Puesto", CorporateReportFormatters.NullSafe(empleado.Puesto), "Posición vigente", CorporateReportPalette.KpiWarning),
            ("Departamento", CorporateReportFormatters.NullSafe(empleado.Departamento), "Área organizacional", CorporateReportPalette.KpiPrimary),
            ("Sucursal", CorporateReportFormatters.NullSafe(empleado.Sucursal), "Centro de trabajo actual", CorporateReportPalette.KpiPurple),
            ("Ingreso", CorporateReportFormatters.FormatDate(empleado.FechaIngreso), "Fecha de alta", CorporateReportPalette.KpiTeal));
    }

    private static void ComposePersonalDataSection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeSection(container, "Datos personales", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(14);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(left, "Fecha de nacimiento", CorporateReportFormatters.FormatDateNullable(empleado.FechaNacimiento));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Teléfono", CorporateReportFormatters.NullSafe(empleado.Telefono));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(right, "Correo", CorporateReportFormatters.NullSafe(empleado.Email));
                    CorporatePdfBlocks.ComposeLabeledField(right, "Empleado ID", empleado.Id.ToString(CultureInfo.InvariantCulture));
                });
            });
        });
    }

    private static void ComposeLaborSection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeSection(container, "Situación laboral", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(14);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(left, "Fecha de baja actual", CorporateReportFormatters.FormatDateNullable(empleado.FechaBajaActual));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Tipo de baja actual", CorporateReportFormatters.FormatNullableLabel(empleado.TipoBajaActual));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(right, "Fecha de reingreso actual", CorporateReportFormatters.FormatDateNullable(empleado.FechaReingresoActual));
                    CorporatePdfBlocks.ComposeLabeledField(right, "Recontratable", CorporateReportFormatters.FormatBoolNullable(empleado.Recontratable));
                });
            });
        });
    }

    private static void ComposeHistorySection(
        IContainer container,
        IReadOnlyList<EmpleadoFichaMovimientoViewModel> movimientos)
    {
        CorporatePdfBlocks.ComposeSection(container, "Historial laboral", body =>
        {
            if (movimientos.Count == 0)
            {
                body.Item().Element(c =>
                    CorporatePdfBlocks.ComposeEmptyState(c, "Sin movimientos laborales registrados."));
                return;
            }

            body.Item().Column(list =>
            {
                list.Spacing(5);

                foreach (var movimiento in movimientos)
                {
                    list.Item().Element(c =>
                    {
                        c.ReportMutedCard(cornerRadius: 6, padding: 8)
                            .Column(card =>
                            {
                                card.Spacing(3);

                                card.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(CorporateReportFormatters.FormatLabel(movimiento.TipoMovimiento))
                                        .FontSize(9.2f)
                                        .SemiBold()
                                        .FontColor(CorporateReportPalette.Ink900);

                                    row.ConstantItem(82).AlignRight().Text(CorporateReportFormatters.FormatDate(movimiento.FechaMovimiento))
                                        .FontSize(8.2f)
                                        .FontColor(CorporateReportPalette.Ink500);
                                });

                                card.Item().Text(text =>
                                {
                                    text.Span("Tipo de baja: ").SemiBold().FontColor(CorporateReportPalette.Ink600);
                                    text.Span(CorporateReportFormatters.FormatNullableLabel(movimiento.TipoBaja)).FontColor(CorporateReportPalette.Ink900);
                                });

                                card.Item().Text(text =>
                                {
                                    text.Span("Motivo: ").SemiBold().FontColor(CorporateReportPalette.Ink600);
                                    text.Span(CorporateReportFormatters.NullSafe(movimiento.Motivo)).FontColor(CorporateReportPalette.Ink900);
                                });

                                if (!string.IsNullOrWhiteSpace(movimiento.Comentario))
                                {
                                    card.Item().Text(text =>
                                    {
                                        text.Span("Comentario: ").SemiBold().FontColor(CorporateReportPalette.Ink600);
                                        text.Span(movimiento.Comentario!.Trim()).FontColor(CorporateReportPalette.Ink900);
                                    });
                                }
                            });
                    });
                }
            });
        });
    }

    private static void ComposeDocumentsSection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeSection(container, "Situación documental", body =>
        {
            body.Item().Element(c =>
                CorporatePdfBlocks.ComposeKpiRow(
                    c,
                    ("Requeridos", RequiredTipos.Length.ToString(CultureInfo.InvariantCulture), "Documentos base", CorporateReportPalette.KpiPrimary),
                    ("Cargados", empleado.DocumentosCargados.ToString(CultureInfo.InvariantCulture), "Disponibles", CorporateReportPalette.KpiSuccess),
                    ("Pendientes", empleado.DocumentosFaltantes.ToString(CultureInfo.InvariantCulture), "Por completar", CorporateReportPalette.KpiWarning),
                    ("Por vencer", empleado.DocumentosPorVencer.ToString(CultureInfo.InvariantCulture), "Atención próxima", CorporateReportPalette.KpiPurple),
                    ("Vencidos", empleado.DocumentosVencidos.ToString(CultureInfo.InvariantCulture), "Requieren acción", CorporateReportPalette.Danger)));

            body.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.4f);   // Documento
                    columns.ConstantColumn(90);     // Estado
                    columns.ConstantColumn(82);     // Vencimiento
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Documento");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Estado");
                    header.Cell().Element(c => c.TableHeaderCell()).Text("Vencimiento");
                });

                for (var index = 0; index < empleado.Documentos.Count; index++)
                {
                    var item = empleado.Documentos[index];
                    var background = CorporatePdfStyles.ZebraRow(index);

                    table.Cell().Element(c => c.TableDataCell(background, emphasize: true))
                        .Text(CorporateReportFormatters.FormatTipoDocumento(item.Tipo));

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(item.Estado);

                    table.Cell().Element(c => c.TableDataCell(background))
                        .Text(CorporateReportFormatters.FormatDateNullable(item.FechaVencimiento));
                }
            });
        });
    }

    private static string BuildFileName(string numEmpleado)
    {
        return CorporateReportFormatters.BuildTimestampedFileName(
            "ficha_empleado",
            "pdf",
            CorporateReportFormatters.NormalizeFileSegment(numEmpleado, "sin_numero"));
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