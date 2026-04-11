using ClosedXML.Excel;
using Gv.Rh.Application.DTOs.Reclutamiento.Reportes;
using Gv.Rh.Application.Interfaces.Reclutamiento;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Gv.Rh.Infrastructure.Services.Reclutamiento;

public sealed class ReclutamientoReporteService : IReclutamientoReporteService
{
    private readonly RhDbContext _db;

    public ReclutamientoReporteService(RhDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> ExportarXlsxAsync(
        ReclutamientoReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var rows = await ObtenerRowsAsync(filtro, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Reclutamiento");

        ws.Cell(1, 1).Value = "VacanteId";
        ws.Cell(1, 2).Value = "Vacante";
        ws.Cell(1, 3).Value = "Departamento";
        ws.Cell(1, 4).Value = "Puesto";
        ws.Cell(1, 5).Value = "Sucursal";
        ws.Cell(1, 6).Value = "Estatus vacante";
        ws.Cell(1, 7).Value = "CandidatoId";
        ws.Cell(1, 8).Value = "Candidato";
        ws.Cell(1, 9).Value = "Teléfono";
        ws.Cell(1, 10).Value = "Correo";
        ws.Cell(1, 11).Value = "Etapa actual";
        ws.Cell(1, 12).Value = "Estatus candidato";
        ws.Cell(1, 13).Value = "Fecha postulación UTC";
        ws.Cell(1, 14).Value = "Último movimiento UTC";
        ws.Cell(1, 15).Value = "Resultado";

        var header = ws.Range(1, 1, 1, 15);
        header.Style.Font.Bold = true;

        for (var i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            var row = i + 2;

            ws.Cell(row, 1).Value = item.VacanteId;
            ws.Cell(row, 2).Value = item.VacanteTitulo;
            ws.Cell(row, 3).Value = item.Departamento;
            ws.Cell(row, 4).Value = item.Puesto;
            ws.Cell(row, 5).Value = item.Sucursal;
            ws.Cell(row, 6).Value = item.EstatusVacante;
            ws.Cell(row, 7).Value = item.CandidatoId;
            ws.Cell(row, 8).Value = item.CandidatoNombre;
            ws.Cell(row, 9).Value = item.Telefono;
            ws.Cell(row, 10).Value = item.Correo;
            ws.Cell(row, 11).Value = item.EtapaActual;
            ws.Cell(row, 12).Value = item.EstatusCandidato;
            ws.Cell(row, 13).Value = item.FechaPostulacionUtc;
            ws.Cell(row, 14).Value = item.FechaUltimaActualizacionUtc;
            ws.Cell(row, 15).Value = item.Resultado;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportarPdfAsync(
        ReclutamientoReporteFiltroDto filtro,
        CancellationToken cancellationToken = default)
    {
        var rows = await ObtenerRowsAsync(filtro, cancellationToken);

        const string brand = "#1F3A5F";
        const string ink = "#111827";
        const string muted = "#6B7280";
        const string border = "#D7DEE8";
        const string surface = "#F6F8FB";
        const string headerBg = "#EAF0F8";

        var total = rows.Count;
        var contratados = rows.Count(x => x.Resultado == "CONTRATADO");
        var descartados = rows.Count(x => x.Resultado == "DESCARTADO");
        var enProceso = rows.Count(x => x.Resultado == "EN PROCESO");

        static string TextoFiltroTexto(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "Todos" : value;

        static string TextoFiltroNumero(int? value) =>
            value.HasValue ? value.Value.ToString() : "Todos";

        static string FechaCorta(DateTime? value) =>
            value.HasValue ? value.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "—";

        static string FechaFiltro(DateOnly? value) =>
            value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "Todos";

        static IContainer Card(IContainer container) =>
            container
                .Border(1)
                .BorderColor(border)
                .Background(surface)
                .Padding(8);

        static IContainer HeaderCell(IContainer container) =>
            container
                .Background(headerBg)
                .BorderBottom(1)
                .BorderColor(border)
                .PaddingVertical(5)
                .PaddingHorizontal(3);

        static IContainer BodyCell(IContainer container) =>
            container
                .BorderBottom(1)
                .BorderColor("#EDF1F5")
                .PaddingVertical(4)
                .PaddingHorizontal(3);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(ink));

                page.Header().Column(column =>
                {
                    column.Spacing(6);

                    column.Item()
                        .Border(1)
                        .BorderColor(border)
                        .Background(surface)
                        .Padding(10)
                        .Column(inner =>
                        {
                            inner.Spacing(3);

                            inner.Item().Text("Reporte de Reclutamiento")
                                .FontSize(16)
                                .Bold()
                                .FontColor(brand);

                            inner.Item().Text("Vacantes, candidatos, etapa actual y resultado")
                                .FontSize(9)
                                .FontColor(muted);

                            inner.Item().Text($"Generado UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}")
                                .FontSize(7)
                                .FontColor(muted);
                        });
                });

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Table(summary =>
                    {
                        summary.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        summary.Cell().Element(Card).Column(card =>
                        {
                            card.Item().Text("Total registros").FontSize(7).FontColor(muted);
                            card.Item().Text(total.ToString()).FontSize(15).Bold().FontColor(brand);
                        });

                        summary.Cell().Element(Card).Column(card =>
                        {
                            card.Item().Text("Contratados").FontSize(7).FontColor(muted);
                            card.Item().Text(contratados.ToString()).FontSize(15).Bold().FontColor("#166534");
                        });

                        summary.Cell().Element(Card).Column(card =>
                        {
                            card.Item().Text("Descartados").FontSize(7).FontColor(muted);
                            card.Item().Text(descartados.ToString()).FontSize(15).Bold().FontColor("#991B1B");
                        });

                        summary.Cell().Element(Card).Column(card =>
                        {
                            card.Item().Text("En proceso").FontSize(7).FontColor(muted);
                            card.Item().Text(enProceso.ToString()).FontSize(15).Bold().FontColor("#92400E");
                        });
                    });

                    column.Item()
                        .Border(1)
                        .BorderColor(border)
                        .Padding(8)
                        .Column(filters =>
                        {
                            filters.Spacing(3);

                            filters.Item().Text("Filtros aplicados")
                                .Bold()
                                .FontSize(9)
                                .FontColor(brand);

                            filters.Item().Text(
                                $"VacanteId: {TextoFiltroNumero(filtro.VacanteId)}   |   " +
                                $"Estatus vacante: {TextoFiltroTexto(filtro.EstatusVacante)}   |   " +
                                $"SucursalId: {TextoFiltroNumero(filtro.SucursalId)}");

                            filters.Item().Text(
                                $"CandidatoId: {TextoFiltroNumero(filtro.CandidatoId)}   |   " +
                                $"Etapa: {TextoFiltroTexto(filtro.Etapa)}   |   " +
                                $"Desde: {FechaFiltro(filtro.FechaDesde)}   |   " +
                                $"Hasta: {FechaFiltro(filtro.FechaHasta)}");
                        });

                    if (rows.Count == 0)
                    {
                        column.Item()
                            .Border(1)
                            .BorderColor(border)
                            .Background(surface)
                            .Padding(14)
                            .Text("No hay registros para los filtros actuales.")
                            .FontColor(muted);
                    }
                    else
                    {
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(34);    // VacanteId
                                columns.RelativeColumn(1.9f);  // Vacante
                                columns.RelativeColumn(1.25f); // Depto/Puesto
                                columns.RelativeColumn(1.0f);  // Sucursal
                                columns.ConstantColumn(48);    // Estatus vacante
                                columns.ConstantColumn(38);    // CandidatoId
                                columns.RelativeColumn(1.8f);  // Candidato
                                columns.RelativeColumn(1.15f); // Contacto
                                columns.ConstantColumn(54);    // Etapa
                                columns.ConstantColumn(56);    // Estatus candidato
                                columns.ConstantColumn(66);    // Postulación
                                columns.ConstantColumn(66);    // Movimiento
                                columns.ConstantColumn(54);    // Resultado
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("VacId").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Vacante").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Depto / Puesto").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Sucursal").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Est. vacante").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("CandId").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Candidato").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Contacto").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Etapa").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Est. cand.").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Postulación").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Movimiento").SemiBold().FontSize(7);
                                header.Cell().Element(HeaderCell).Text("Resultado").SemiBold().FontSize(7);
                            });

                            foreach (var item in rows)
                            {
                                table.Cell().Element(BodyCell).Text(item.VacanteId.ToString()).FontSize(7);
                                table.Cell().Element(BodyCell).Text(item.VacanteTitulo ?? "—").FontSize(7);

                                table.Cell().Element(BodyCell).Column(c =>
                                {
                                    c.Spacing(1);
                                    c.Item().Text(item.Departamento ?? "—").FontSize(7);
                                    c.Item().Text(item.Puesto ?? "—").FontSize(6).FontColor(muted);
                                });

                                table.Cell().Element(BodyCell).Text(item.Sucursal ?? "—").FontSize(7);
                                table.Cell().Element(BodyCell).Text(item.EstatusVacante ?? "—").FontSize(7);
                                table.Cell().Element(BodyCell).Text(item.CandidatoId.ToString()).FontSize(7);
                                table.Cell().Element(BodyCell).Text(item.CandidatoNombre ?? "—").FontSize(7);

                                table.Cell().Element(BodyCell).Column(c =>
                                {
                                    c.Spacing(1);
                                    c.Item().Text(item.Telefono ?? "—").FontSize(7);
                                    c.Item().Text(item.Correo ?? "—").FontSize(6).FontColor(muted);
                                });

                                table.Cell().Element(BodyCell).Text(item.EtapaActual ?? "—").FontSize(7);
                                table.Cell().Element(BodyCell).Text(item.EstatusCandidato ?? "—").FontSize(7);
                                table.Cell().Element(BodyCell).Text(FechaCorta(item.FechaPostulacionUtc)).FontSize(7);
                                table.Cell().Element(BodyCell).Text(FechaCorta(item.FechaUltimaActualizacionUtc)).FontSize(7);
                                table.Cell().Element(BodyCell).Text(item.Resultado ?? "—").FontSize(7);
                            }
                        });
                    }
                });

                page.Footer()
                    .BorderTop(1)
                    .BorderColor(border)
                    .PaddingTop(4)
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Text($"GV RH · Reclutamiento · {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                            .FontSize(7)
                            .FontColor(muted);

                        row.ConstantItem(90).AlignRight().Text(text =>
                        {
                            text.Span("Página ").FontSize(7).FontColor(muted);
                            text.CurrentPageNumber().FontSize(7).SemiBold();
                            text.Span(" de ").FontSize(7).FontColor(muted);
                            text.TotalPages().FontSize(7).SemiBold();
                        });
                    });
            });
        }).GeneratePdf();
    }

    private async Task<List<ReclutamientoReporteRowDto>> ObtenerRowsAsync(
        ReclutamientoReporteFiltroDto filtro,
        CancellationToken cancellationToken)
    {
        return await ConstruirQuery(filtro)
            .OrderBy(x => x.VacanteTitulo)
            .ThenBy(x => x.CandidatoNombre)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ReclutamientoReporteRowDto> ConstruirQuery(ReclutamientoReporteFiltroDto filtro)
    {
        var query =
            from p in _db.Postulaciones.AsNoTracking()
            join v in _db.Vacantes.AsNoTracking() on p.VacanteId equals v.Id
            join c in _db.Candidatos.AsNoTracking() on p.CandidatoId equals c.Id
            join s in _db.Sucursales.AsNoTracking() on v.SucursalId equals s.Id into sucursales
            from s in sucursales.DefaultIfEmpty()
            join d in _db.Departamentos.AsNoTracking() on v.DepartamentoId equals d.Id into departamentos
            from d in departamentos.DefaultIfEmpty()
            join pu in _db.Puestos.AsNoTracking() on v.PuestoId equals pu.Id into puestos
            from pu in puestos.DefaultIfEmpty()
            select new ReclutamientoReporteRowDto
            {
                VacanteId = v.Id,
                VacanteTitulo = v.Titulo,
                SucursalId = v.SucursalId,
                Departamento = d != null ? d.Nombre : null,
                Puesto = pu != null ? pu.Nombre : null,
                Sucursal = s != null ? s.Nombre : null,
                EstatusVacante = v.Estatus.ToString(),

                CandidatoId = c.Id,
                CandidatoNombre = c.Nombres + " " + c.ApellidoPaterno + (c.ApellidoMaterno != null ? " " + c.ApellidoMaterno : ""),
                Telefono = c.Telefono,
                Correo = c.Email,

                EtapaActual = p.EtapaActual.ToString(),
                EstatusCandidato = p.EsContratado
                    ? "CONTRATADO"
                    : !string.IsNullOrWhiteSpace(p.MotivoDescarte)
                        ? "DESCARTADO"
                        : p.Activo
                            ? "EN_PROCESO"
                            : "INACTIVO",

                FechaPostulacionUtc = p.FechaPostulacionUtc,
                FechaUltimaActualizacionUtc = p.FechaUltimoMovimientoUtc,

                Resultado = p.EsContratado
                    ? "CONTRATADO"
                    : !string.IsNullOrWhiteSpace(p.MotivoDescarte)
                        ? "DESCARTADO"
                        : "EN PROCESO"
            };

        if (filtro.VacanteId.HasValue)
            query = query.Where(x => x.VacanteId == filtro.VacanteId.Value);

        if (filtro.CandidatoId.HasValue)
            query = query.Where(x => x.CandidatoId == filtro.CandidatoId.Value);

        if (filtro.SucursalId.HasValue)
            query = query.Where(x => x.SucursalId == filtro.SucursalId.Value);

        if (!string.IsNullOrWhiteSpace(filtro.EstatusVacante))
            query = query.Where(x => x.EstatusVacante == filtro.EstatusVacante);

        if (!string.IsNullOrWhiteSpace(filtro.Etapa))
            query = query.Where(x => x.EtapaActual == filtro.Etapa);

        if (filtro.FechaDesde.HasValue)
        {
            var desde = filtro.FechaDesde.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(x => x.FechaPostulacionUtc >= desde);
        }

        if (filtro.FechaHasta.HasValue)
        {
            var hasta = filtro.FechaHasta.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(x => x.FechaPostulacionUtc <= hasta);
        }

        return query;
    }
}