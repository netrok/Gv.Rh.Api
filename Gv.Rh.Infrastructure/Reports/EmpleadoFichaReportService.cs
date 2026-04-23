using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Common;
using Gv.Rh.Domain.Common;
using Gv.Rh.Infrastructure.Persistence;
using Gv.Rh.Infrastructure.Reports.Common;
using Gv.Rh.Infrastructure.Reports.Pdf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace Gv.Rh.Infrastructure.Reports;

public sealed class EmpleadoFichaReportService : IEmpleadoFichaReportService
{
    private readonly RhDbContext _context;
    private readonly IWebHostEnvironment _environment;

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

    public EmpleadoFichaReportService(RhDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
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
                Puesto = x.Puesto != null ? x.Puesto.Nombre : null,

                Curp = x.Curp,
                Rfc = x.Rfc,
                Nss = x.Nss,
                Sexo = x.Sexo.ToString(),
                EstadoCivil = x.EstadoCivil.ToString(),
                Nacionalidad = x.Nacionalidad,

                DireccionCalle = x.DireccionCalle,
                DireccionNumeroExterior = x.DireccionNumeroExterior,
                DireccionNumeroInterior = x.DireccionNumeroInterior,
                DireccionColonia = x.DireccionColonia,
                DireccionCiudad = x.DireccionCiudad,
                DireccionEstado = x.DireccionEstado,
                DireccionCodigoPostal = x.DireccionCodigoPostal,
                CodigoPostalFiscal = x.CodigoPostalFiscal,

                ContactoEmergenciaNombre = x.ContactoEmergenciaNombre,
                ContactoEmergenciaTelefono = x.ContactoEmergenciaTelefono,
                ContactoEmergenciaParentesco = x.ContactoEmergenciaParentesco,

                FotoNombreOriginal = x.FotoNombreOriginal,
                FotoRutaRelativa = x.FotoRutaRelativa,
                FotoMimeType = x.FotoMimeType,
                FotoUpdatedAtUtc = x.FotoUpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (empleado is null)
            throw new InvalidOperationException("No se encontró el empleado solicitado.");

        empleado.FotoBytes = LoadEmployeePhotoBytes(empleado.FotoRutaRelativa);

        var movimientos = await _context.EmpleadoMovimientosLaborales
            .AsNoTracking()
            .Where(x => x.EmpleadoId == empleadoId)
            .OrderByDescending(x => x.FechaMovimiento)
            .ThenByDescending(x => x.CreatedAtUtc)
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

                    column.Item().Element(container => ComposeIdentitySection(container, empleado));
                    column.Item().Element(container => ComposeSummarySection(container, empleado));
                    column.Item().Element(container => ComposeGeneralDataSection(container, empleado));
                    column.Item().Element(container => ComposeOfficialDataSection(container, empleado));
                    column.Item().Element(container => ComposeAddressSection(container, empleado));
                    column.Item().Element(container => ComposeEmergencyContactSection(container, empleado));
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

    private byte[]? LoadEmployeePhotoBytes(string? fotoRutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(fotoRutaRelativa))
            return null;

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");

        var normalizedRelativePath = fotoRutaRelativa
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var fullPath = Path.Combine(webRoot, normalizedRelativePath);

        if (!File.Exists(fullPath))
            return null;

        try
        {
            return File.ReadAllBytes(fullPath);
        }
        catch
        {
            return null;
        }
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

    private static void ComposeIdentitySection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeSection(container, "Identificación general", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(12);

                row.RelativeItem(3).Column(left =>
                {
                    left.Spacing(6);

                    left.Item().Text(CorporateReportFormatters.CombineFullName(
                            empleado.Nombres,
                            empleado.ApellidoPaterno,
                            empleado.ApellidoMaterno))
                        .FontSize(15)
                        .SemiBold()
                        .FontColor(CorporateReportPalette.Ink900);

                    left.Item().Row(chips =>
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

                    left.Item().Row(fields =>
                    {
                        fields.Spacing(12);

                        fields.RelativeItem().Column(col =>
                        {
                            col.Spacing(4);
                            CorporatePdfBlocks.ComposeLabeledField(col, "Número de empleado", CorporateReportFormatters.NullSafe(empleado.NumEmpleado));
                            CorporatePdfBlocks.ComposeLabeledField(col, "Puesto actual", CorporateReportFormatters.NullSafe(empleado.Puesto));
                            CorporatePdfBlocks.ComposeLabeledField(col, "Departamento", CorporateReportFormatters.NullSafe(empleado.Departamento));
                        });

                        fields.RelativeItem().Column(col =>
                        {
                            col.Spacing(4);
                            CorporatePdfBlocks.ComposeLabeledField(col, "Sucursal", CorporateReportFormatters.NullSafe(empleado.Sucursal));
                            CorporatePdfBlocks.ComposeLabeledField(col, "Fecha de ingreso", CorporateReportFormatters.FormatDate(empleado.FechaIngreso));
                            CorporatePdfBlocks.ComposeLabeledField(col, "Correo", CorporateReportFormatters.NullSafe(empleado.Email));
                        });
                    });
                });

                row.ConstantItem(110).Element(c => ComposePhotoBlock(c, empleado));
            });
        });
    }

    private static void ComposePhotoBlock(IContainer container, EmpleadoFichaViewModel empleado)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Background(Colors.White)
            .Padding(6)
            .Column(column =>
            {
                column.Spacing(4);

                column.Item().AlignCenter().Text("Fotografía")
                    .FontSize(8.5f)
                    .SemiBold()
                    .FontColor(CorporateReportPalette.Ink600);

                column.Item().Height(120).Element(photoContainer =>
                {
                    var frame = photoContainer
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten3)
                        .Background(Colors.Grey.Lighten5)
                        .AlignMiddle()
                        .AlignCenter();

                    if (empleado.FotoBytes is { Length: > 0 })
                    {
                        frame.Padding(2).Image(empleado.FotoBytes).FitArea();
                        return;
                    }

                    frame.Text("Sin fotografía registrada")
                        .FontSize(8)
                        .FontColor(CorporateReportPalette.Ink500)
                        .AlignCenter();
                });

                column.Item().AlignCenter().Text(CorporateReportFormatters.NullSafe(empleado.NumEmpleado))
                    .FontSize(7.8f)
                    .FontColor(CorporateReportPalette.Ink500);
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

    private static void ComposeGeneralDataSection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeSection(container, "Datos generales", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(14);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(left, "Nombres", CorporateReportFormatters.NullSafe(empleado.Nombres));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Apellido paterno", CorporateReportFormatters.NullSafe(empleado.ApellidoPaterno));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Apellido materno", CorporateReportFormatters.NullSafe(empleado.ApellidoMaterno));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(right, "Fecha de nacimiento", CorporateReportFormatters.FormatDateNullable(empleado.FechaNacimiento));
                    CorporatePdfBlocks.ComposeLabeledField(right, "Teléfono", CorporateReportFormatters.NullSafe(empleado.Telefono));
                    CorporatePdfBlocks.ComposeLabeledField(right, "Correo", CorporateReportFormatters.NullSafe(empleado.Email));
                });
            });
        });
    }

    private static void ComposeOfficialDataSection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeSection(container, "Identificación oficial", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(14);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(left, "CURP", CorporateReportFormatters.NullSafe(empleado.Curp));
                    CorporatePdfBlocks.ComposeLabeledField(left, "RFC", CorporateReportFormatters.NullSafe(empleado.Rfc));
                    CorporatePdfBlocks.ComposeLabeledField(left, "NSS", CorporateReportFormatters.NullSafe(empleado.Nss));
                });

                row.RelativeItem().Column(center =>
                {
                    center.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(center, "Sexo", CorporateReportFormatters.FormatNullableLabel(empleado.Sexo));
                    CorporatePdfBlocks.ComposeLabeledField(center, "Estado civil", CorporateReportFormatters.FormatNullableLabel(empleado.EstadoCivil));
                    CorporatePdfBlocks.ComposeLabeledField(center, "Nacionalidad", CorporateReportFormatters.NullSafe(empleado.Nacionalidad));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(right, "Código postal fiscal", CorporateReportFormatters.NullSafe(empleado.CodigoPostalFiscal));
                });
            });
        });
    }

    private static void ComposeAddressSection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeSection(container, "Domicilio", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(14);

                row.RelativeItem(1.35f).Column(left =>
                {
                    left.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(left, "Calle", CorporateReportFormatters.NullSafe(empleado.DireccionCalle));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Colonia", CorporateReportFormatters.NullSafe(empleado.DireccionColonia));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Ciudad / municipio", CorporateReportFormatters.NullSafe(empleado.DireccionCiudad));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Estado", CorporateReportFormatters.NullSafe(empleado.DireccionEstado));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(right, "Número exterior", CorporateReportFormatters.NullSafe(empleado.DireccionNumeroExterior));
                    CorporatePdfBlocks.ComposeLabeledField(right, "Número interior", CorporateReportFormatters.NullSafe(empleado.DireccionNumeroInterior));
                    CorporatePdfBlocks.ComposeLabeledField(right, "Código postal", CorporateReportFormatters.NullSafe(empleado.DireccionCodigoPostal));
                });
            });
        });
    }

    private static void ComposeEmergencyContactSection(IContainer container, EmpleadoFichaViewModel empleado)
    {
        CorporatePdfBlocks.ComposeSection(container, "Contacto de emergencia", body =>
        {
            body.Item().Row(row =>
            {
                row.Spacing(14);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(left, "Nombre", CorporateReportFormatters.NullSafe(empleado.ContactoEmergenciaNombre));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Parentesco", CorporateReportFormatters.NullSafe(empleado.ContactoEmergenciaParentesco));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(right, "Teléfono", CorporateReportFormatters.NullSafe(empleado.ContactoEmergenciaTelefono));
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
                    CorporatePdfBlocks.ComposeLabeledField(left, "Estatus laboral actual", CorporateReportFormatters.FormatLabel(empleado.EstatusLaboralActual));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Fecha de baja actual", CorporateReportFormatters.FormatDateNullable(empleado.FechaBajaActual));
                    CorporatePdfBlocks.ComposeLabeledField(left, "Tipo de baja actual", CorporateReportFormatters.FormatNullableLabel(empleado.TipoBajaActual));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(4);
                    CorporatePdfBlocks.ComposeLabeledField(right, "Fecha de reingreso actual", CorporateReportFormatters.FormatDateNullable(empleado.FechaReingresoActual));
                    CorporatePdfBlocks.ComposeLabeledField(right, "Recontratable", CorporateReportFormatters.FormatBoolNullable(empleado.Recontratable));
                    CorporatePdfBlocks.ComposeLabeledField(right, "Activo", CorporateReportFormatters.FormatBool(empleado.Activo));
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
                    columns.RelativeColumn(2.4f);
                    columns.ConstantColumn(90);
                    columns.ConstantColumn(82);
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

        public string? Curp { get; set; }
        public string? Rfc { get; set; }
        public string? Nss { get; set; }
        public string? Sexo { get; set; }
        public string? EstadoCivil { get; set; }
        public string? Nacionalidad { get; set; }

        public string? DireccionCalle { get; set; }
        public string? DireccionNumeroExterior { get; set; }
        public string? DireccionNumeroInterior { get; set; }
        public string? DireccionColonia { get; set; }
        public string? DireccionCiudad { get; set; }
        public string? DireccionEstado { get; set; }
        public string? DireccionCodigoPostal { get; set; }
        public string? CodigoPostalFiscal { get; set; }

        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
        public string? ContactoEmergenciaParentesco { get; set; }

        public string? FotoNombreOriginal { get; set; }
        public string? FotoRutaRelativa { get; set; }
        public string? FotoMimeType { get; set; }
        public DateTime? FotoUpdatedAtUtc { get; set; }
        public byte[]? FotoBytes { get; set; }

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