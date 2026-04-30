using ExcelDataReader;
using Gv.Rh.Application.DTOs.Vacaciones.Import;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Gv.Rh.Infrastructure.Services;

public sealed class VacacionesLegacyImportService : IVacacionesLegacyImportService
{
    private readonly RhDbContext _db;

    private static readonly Regex PagoDiasRegex =
        new(@"\b(VTA|VENTA|VENDE|VENDIO|VENDIÓ)\b[^\d]*(\d+(?:[.,]\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex YearRegex =
        new(@"\b(20\d{2}|19\d{2})\b", RegexOptions.Compiled);

    public VacacionesLegacyImportService(RhDbContext db)
    {
        _db = db;
    }

    public async Task<VacacionesLegacyImportPreviewDto> PreviewAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (stream is null)
            throw new InvalidDataException("No se recibió un archivo Excel válido.");

        if (stream.CanSeek)
            stream.Position = 0;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var employees = await _db.Empleados
            .AsNoTracking()
            .Select(x => new EmployeeLookupRow
            {
                Id = x.Id,
                NumEmpleado = x.NumEmpleado,
                Nombres = x.Nombres,
                ApellidoPaterno = x.ApellidoPaterno,
                ApellidoMaterno = x.ApellidoMaterno,
                Rfc = x.Rfc,
                Nss = x.Nss,
                FechaIngreso = x.FechaIngreso
            })
            .ToListAsync(cancellationToken);

        var saldosByEmpleado = await _db.VacacionPeriodos
            .AsNoTracking()
            .GroupBy(x => x.EmpleadoId)
            .Select(x => new SaldoSistemaEmpleado
            {
                EmpleadoId = x.Key,
                Saldo = x.Sum(p => p.Saldo),
                Periodos = x.Count()
            })
            .ToDictionaryAsync(x => x.EmpleadoId, x => x, cancellationToken);

        using var reader = ExcelReaderFactory.CreateReader(
            stream,
            new ExcelReaderConfiguration
            {
                FallbackEncoding = Encoding.GetEncoding(1252)
            });

        var result = new VacacionesLegacyImportPreviewDto
        {
            Archivo = fileName
        };

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            result.TotalHojas++;

            var sheetName = reader.Name ?? $"Hoja {result.TotalHojas}";
            var rows = ReadSheetRows(reader);

            if (ShouldIgnoreSheet(sheetName, rows))
                continue;

            var item = AnalyzeSheet(sheetName, rows, employees, saldosByEmpleado);
            if (item is null)
                continue;

            result.HojasAnalizadas++;
            result.Items.Add(item);
        }
        while (reader.NextResult());

        result.EmpleadosDetectados = result.Items.Count(x => x.EmpleadoEncontrado);
        result.EmpleadosNoEncontrados = result.Items.Count(x => !x.EmpleadoEncontrado);
        result.ConDiferencias = result.Items.Count(x => x.Diferencia.HasValue && x.Diferencia.Value != 0m);

        if (result.Items.Count == 0)
        {
            result.Advertencias.Add("No se detectaron hojas de empleados con estructura de vacaciones. Revisa si el archivo corresponde al formato legacy de nóminas.");
        }

        if (result.EmpleadosNoEncontrados > 0)
        {
            result.Advertencias.Add("Hay hojas que no pudieron empatarse con empleados del sistema. Revisa número de empleado, RFC, NSS o nombre.");
        }

        if (result.ConDiferencias > 0)
        {
            result.Advertencias.Add("Hay saldos del Excel que difieren del saldo actual del sistema. No confirmes importación sin revisión de RH/Nóminas.");
        }

        return result;
    }

    public async Task<VacacionesLegacyImportConfirmResultDto> ConfirmAsync(
        Stream stream,
        string fileName,
        VacacionesLegacyImportConfirmRequestDto request,
        int? usuarioResponsableId,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var preview = await PreviewAsync(stream, fileName, cancellationToken);

        var idsConfirmados = request.EmpleadoIdsConfirmados
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();

        var result = new VacacionesLegacyImportConfirmResultDto
        {
            Archivo = fileName,
            TotalPreviewItems = preview.Items.Count,
            Solicitados = request.ImportarTodosElegibles
                ? preview.Items.Count(x => x.PuedeImportar)
                : idsConfirmados.Count
        };

        if (!request.ImportarTodosElegibles && idsConfirmados.Count == 0)
        {
            result.Advertencias.Add("No se seleccionaron empleados para confirmar. Envía EmpleadoIdsConfirmados o activa ImportarTodosElegibles.");
            return result;
        }

        var candidatos = preview.Items
            .Where(x => x.EmpleadoId.HasValue)
            .Where(x => request.ImportarTodosElegibles || idsConfirmados.Contains(x.EmpleadoId!.Value))
            .OrderBy(x => x.NumEmpleadoSistema)
            .ThenBy(x => x.NombreSistema)
            .ToList();

        if (candidatos.Count == 0)
        {
            result.Advertencias.Add("No se encontraron registros candidatos para importar según la selección enviada.");
            return result;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var now = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var item in candidatos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var confirmItem = new VacacionesLegacyImportConfirmItemDto
            {
                Hoja = item.Hoja,
                EmpleadoId = item.EmpleadoId,
                NumEmpleado = item.NumEmpleadoSistema,
                NombreEmpleado = item.NombreSistema,
                SaldoExcel = item.SaldoExcel
            };

            result.Items.Add(confirmItem);

            if (!item.EmpleadoId.HasValue)
            {
                confirmItem.Importado = false;
                confirmItem.Accion = "OMITIDO";
                confirmItem.Error = "El registro no tiene empleado ligado.";
                continue;
            }

            if (!item.PuedeImportar)
            {
                confirmItem.Importado = false;
                confirmItem.Accion = "OMITIDO";
                confirmItem.Error = item.Error ?? "El registro no está marcado como importable en el preview.";
                continue;
            }

            if (!item.SaldoExcel.HasValue)
            {
                confirmItem.Importado = false;
                confirmItem.Accion = "OMITIDO";
                confirmItem.Error = "No se detectó saldo Excel confiable.";
                continue;
            }

            if (item.SaldoExcel.Value < 0m && !request.PermitirSaldosNegativos)
            {
                confirmItem.Importado = false;
                confirmItem.Accion = "OMITIDO";
                confirmItem.Error = "El saldo Excel es negativo. Requiere confirmación especial.";
                continue;
            }

            var empleado = await _db.Empleados
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == item.EmpleadoId.Value, cancellationToken);

            if (empleado is null)
            {
                confirmItem.Importado = false;
                confirmItem.Accion = "OMITIDO";
                confirmItem.Error = "El empleado ya no existe en el sistema.";
                continue;
            }

            var hasPeriodos = await _db.VacacionPeriodos
                .AsNoTracking()
                .AnyAsync(x => x.EmpleadoId == empleado.Id, cancellationToken);

            if (hasPeriodos)
            {
                confirmItem.Importado = false;
                confirmItem.Accion = "OMITIDO";
                confirmItem.Error = "El empleado ya tiene periodos de vacaciones en el sistema.";
                continue;
            }

            var politicaId = await _db.VacacionPoliticas
                .AsNoTracking()
                .Where(x => x.Activo)
                .OrderByDescending(x => x.VigenteDesde)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var anioServicio = item.AnioServicioSugerido.GetValueOrDefault(CalculateCurrentServiceYear(empleado.FechaIngreso));
            anioServicio = Math.Max(1, anioServicio);

            var fechaInicio = empleado.FechaIngreso.AddYears(anioServicio - 1);
            var fechaFin = fechaInicio.AddYears(1).AddDays(-1);
            var fechaLimiteDisfrute = fechaFin.AddMonths(6);

            var comentarioBase = BuildImportComment(
                request.Comentario,
                item.ObservacionesOriginales);

            var saldo = item.SaldoExcel.Value;

            var periodo = new VacacionPeriodo
            {
                EmpleadoId = empleado.Id,
                VacacionPoliticaId = politicaId,
                AnioServicio = anioServicio,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                FechaLimiteDisfrute = fechaLimiteDisfrute,
                DiasDerecho = saldo,
                DiasTomados = 0m,
                DiasPagados = 0m,
                DiasAjustados = 0m,
                DiasVencidos = 0m,
                Saldo = saldo,
                PrimaPagada = false,
                FechaPagoPrima = null,
                Comentario = "Saldo inicial histórico importado desde Excel legacy de nóminas.",
                Estatus = EstatusVacacionPeriodo.ABIERTO,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.VacacionPeriodos.Add(periodo);
            await _db.SaveChangesAsync(cancellationToken);

            var movimiento = new VacacionMovimiento
            {
                EmpleadoId = empleado.Id,
                VacacionPeriodoId = periodo.Id,
                TipoMovimiento = TipoMovimientoVacacion.SALDO_INICIAL,
                FechaMovimiento = today,
                FechaInicioDisfrute = null,
                FechaFinDisfrute = null,
                Dias = saldo,
                SaldoAntes = 0m,
                SaldoDespues = saldo,
                Referencia = "IMPORTACION_LEGACY",
                Comentario = comentarioBase,
                UsuarioResponsableId = usuarioResponsableId,
                Origen = "EXCEL_LEGACY",
                ImportacionArchivo = fileName,
                ImportacionHoja = item.Hoja,
                ImportacionFila = item.FilaReferencia,
                CreatedAtUtc = now
            };

            _db.VacacionMovimientos.Add(movimiento);
            await _db.SaveChangesAsync(cancellationToken);

            confirmItem.Importado = true;
            confirmItem.Accion = "IMPORTADO_SALDO_INICIAL";
            confirmItem.SaldoImportado = saldo;
            confirmItem.VacacionPeriodoId = periodo.Id;
            confirmItem.VacacionMovimientoId = movimiento.Id;
            confirmItem.Mensaje = $"Saldo inicial importado: {saldo:0.##} días.";
        }

        await transaction.CommitAsync(cancellationToken);

        result.Importados = result.Items.Count(x => x.Importado);
        result.Omitidos = result.Items.Count(x => !x.Importado && string.IsNullOrWhiteSpace(x.Error));
        result.Errores = result.Items.Count(x => !x.Importado && !string.IsNullOrWhiteSpace(x.Error));

        if (result.Errores > 0)
        {
            result.Advertencias.Add("Algunos registros no fueron importados. Revisa el detalle de errores.");
        }

        return result;
    }

    private static string BuildImportComment(string? comentarioUsuario, string? observacionesOriginales)
    {
        var parts = new List<string>
        {
            "Saldo inicial histórico importado desde Excel legacy de nóminas."
        };

        if (!string.IsNullOrWhiteSpace(comentarioUsuario))
            parts.Add($"Comentario de confirmación: {comentarioUsuario.Trim()}");

        if (!string.IsNullOrWhiteSpace(observacionesOriginales))
            parts.Add($"Observaciones Excel: {observacionesOriginales.Trim()}");

        return NormalizeLongText(string.Join(" ", parts))!;
    }

    private static List<List<string?>> ReadSheetRows(IExcelDataReader reader)
    {
        var rows = new List<List<string?>>();

        while (reader.Read())
        {
            var cells = new List<string?>();
            var hasValue = false;

            for (var col = 0; col < reader.FieldCount; col++)
            {
                var value = NormalizeCellValue(reader.GetValue(col));
                cells.Add(value);

                if (!string.IsNullOrWhiteSpace(value))
                    hasValue = true;
            }

            if (hasValue)
                rows.Add(cells);
        }

        return rows;
    }

    private static VacacionesLegacyImportPreviewItemDto? AnalyzeSheet(
        string sheetName,
        List<List<string?>> rows,
        List<EmployeeLookupRow> employees,
        Dictionary<int, SaldoSistemaEmpleado> saldosByEmpleado)
    {
        if (rows.Count == 0)
            return null;

        var header = ExtractEmployeeHeader(sheetName, rows);
        var vacationTable = ExtractVacationTable(rows);

        var item = new VacacionesLegacyImportPreviewItemDto
        {
            Hoja = sheetName,
            FilaReferencia = vacationTable.FilaReferencia,
            NumEmpleadoExcel = header.NumEmpleado,
            NombreExcel = header.Nombre,
            RfcExcel = header.Rfc,
            NssExcel = header.Nss,
            PuestoExcel = header.Puesto,
            FechaIngresoExcel = header.FechaIngreso,
            DiasDerechoExcel = vacationTable.DiasDerecho,
            DiasDisfrutadosExcel = vacationTable.DiasDisfrutados,
            DiasPagadosExcel = vacationTable.DiasPagados,
            SaldoExcel = vacationTable.Saldo,
            ObservacionesOriginales = vacationTable.Observaciones
        };

        var empleado = FindEmpleado(header, employees);
        if (empleado is null)
        {
            item.EmpleadoEncontrado = false;
            item.AccionSugerida = "EMPLEADO_NO_ENCONTRADO";
            item.PuedeImportar = false;
            item.Error = "No se encontró empleado por número, RFC, NSS o nombre.";
            return item;
        }

        item.EmpleadoEncontrado = true;
        item.EmpleadoId = empleado.Id;
        item.NumEmpleadoSistema = empleado.NumEmpleado;
        item.NombreSistema = empleado.NombreCompleto;

        var anioServicio = CalculateCurrentServiceYear(empleado.FechaIngreso);
        item.AnioServicioSugerido = anioServicio;

        if (saldosByEmpleado.TryGetValue(empleado.Id, out var saldoSistema))
        {
            item.SaldoSistemaActual = saldoSistema.Saldo;
            item.TienePeriodoSistema = saldoSistema.Periodos > 0;
        }
        else
        {
            item.SaldoSistemaActual = 0m;
            item.TienePeriodoSistema = false;
        }

        if (item.SaldoExcel.HasValue && item.SaldoSistemaActual.HasValue)
            item.Diferencia = item.SaldoExcel.Value - item.SaldoSistemaActual.Value;

        if (!item.SaldoExcel.HasValue)
        {
            item.AccionSugerida = "REVISION_MANUAL";
            item.PuedeImportar = false;
            item.Error = "No se detectó saldo numérico confiable en la hoja.";
            return item;
        }

        if (item.TienePeriodoSistema)
        {
            item.AccionSugerida = item.Diferencia == 0m
                ? "SIN_CAMBIOS"
                : "REVISAR_DIFERENCIA_CON_SISTEMA";

            item.PuedeImportar = false;
            item.Error = "El empleado ya tiene periodos de vacaciones en el sistema. Primero revisa si corresponde ajuste o no importación.";
            return item;
        }

        item.AccionSugerida = "IMPORTAR_SALDO_INICIAL";
        item.PuedeImportar = true;

        return item;
    }

    private static HeaderData ExtractEmployeeHeader(string sheetName, List<List<string?>> rows)
    {
        var data = new HeaderData
        {
            Nombre = LooksLikeEmployeeSheetName(sheetName) ? sheetName.Trim() : null
        };

        foreach (var row in rows.Take(25))
        {
            for (var col = 0; col < row.Count; col++)
            {
                var cell = row[col];
                if (string.IsNullOrWhiteSpace(cell))
                    continue;

                var key = NormalizeKey(cell);

                if (data.NumEmpleado is null && ContainsAny(key, "NOMINA", "NOM", "NUMEROEMPLEADO", "NUMEMPLEADO"))
                    data.NumEmpleado = GetNeighborValue(row, col);

                if (data.Nombre is null && ContainsAny(key, "NOMBRE"))
                    data.Nombre = GetNeighborValue(row, col);

                if (data.Rfc is null && ContainsAny(key, "RFC"))
                    data.Rfc = NormalizeUpper(GetNeighborValue(row, col));

                if (data.Nss is null && ContainsAny(key, "NSS", "SEGUROSOCIAL", "IMSS"))
                    data.Nss = DigitsOnly(GetNeighborValue(row, col));

                if (data.Puesto is null && ContainsAny(key, "PUESTO"))
                    data.Puesto = GetNeighborValue(row, col);

                if (data.FechaIngreso is null && ContainsAny(key, "FECHAINGRESO", "INGRESO"))
                    data.FechaIngreso = GetNeighborValue(row, col);
            }
        }

        data.NumEmpleado = DigitsOnly(data.NumEmpleado) ?? NormalizeUpper(data.NumEmpleado);
        data.Nombre = NormalizeNameText(data.Nombre);
        data.Rfc = NormalizeUpper(data.Rfc);
        data.Nss = DigitsOnly(data.Nss);

        return data;
    }

    private static VacationTableData ExtractVacationTable(List<List<string?>> rows)
    {
        var result = new VacationTableData();

        var headerRowIndex = FindVacationHeaderRow(rows);
        if (headerRowIndex is null)
        {
            result.Observaciones = ExtractRelevantObservations(rows);
            result.Saldo = FindLastNumberNearLabel(rows, "SALDO");
            result.DiasDerecho = FindLastNumberNearLabel(rows, "DIASDEVAC");
            result.DiasDisfrutados = FindLastNumberNearLabel(rows, "DISFRUT");
            result.DiasPagados = ExtractPaidDaysFromText(result.Observaciones);
            return result;
        }

        var headerRow = rows[headerRowIndex.Value];
        var conceptoCol = FindColumn(headerRow, "CONCEPTO", "PERIODO");
        var diasVacCol = FindColumn(headerRow, "DIASDEVAC", "DIASVAC", "VACACIONES");
        var disfrutadosCol = FindColumn(headerRow, "DISFRUT", "TOMADOS", "GOZADOS");
        var saldoCol = FindColumn(headerRow, "SALDO");
        var observacionesCol = FindColumn(headerRow, "OBSERV");

        var observations = new List<string>();

        for (var rowIndex = headerRowIndex.Value + 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];

            var concepto = GetAt(row, conceptoCol);
            var observacion = GetAt(row, observacionesCol);
            var rowText = string.Join(" ", row.Where(x => !string.IsNullOrWhiteSpace(x)));

            if (!string.IsNullOrWhiteSpace(observacion))
                observations.Add(observacion!);
            else if (ContainsAny(NormalizeKey(rowText), "TOMO", "DISFRUT", "VTA", "VENTA", "PERIODO", "SALDO", "PRIMA"))
                observations.Add(rowText);

            var saldo = TryParseDecimal(GetAt(row, saldoCol));
            if (saldo.HasValue)
            {
                result.Saldo = saldo;
                result.FilaReferencia = rowIndex + 1;
            }

            var diasVac = TryParseDecimal(GetAt(row, diasVacCol));
            if (diasVac.HasValue)
                result.DiasDerecho = diasVac;

            var disfrutados = TryParseDecimal(GetAt(row, disfrutadosCol));
            if (disfrutados.HasValue)
                result.DiasDisfrutados = disfrutados;

            var paidFromText = ExtractPaidDaysFromText($"{concepto} {observacion} {rowText}");
            if (paidFromText.HasValue)
                result.DiasPagados = (result.DiasPagados ?? 0m) + paidFromText.Value;
        }

        result.Observaciones = NormalizeLongText(string.Join(" | ", observations.Distinct().TakeLast(12)));
        result.DiasPagados ??= ExtractPaidDaysFromText(result.Observaciones);

        return result;
    }

    private static int? FindVacationHeaderRow(List<List<string?>> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var normalized = string.Join(" ", rows[i].Select(NormalizeKey));

            if (normalized.Contains("CONCEPTO") &&
                (normalized.Contains("SALDO") || normalized.Contains("DISFRUT") || normalized.Contains("DIASDEVAC")))
            {
                return i;
            }
        }

        return null;
    }

    private static EmployeeLookupRow? FindEmpleado(HeaderData header, List<EmployeeLookupRow> employees)
    {
        var numEmpleado = NormalizeUpper(header.NumEmpleado);
        if (!string.IsNullOrWhiteSpace(numEmpleado))
        {
            var byNum = employees.FirstOrDefault(x => NormalizeUpper(x.NumEmpleado) == numEmpleado);
            if (byNum is not null)
                return byNum;
        }

        var rfc = NormalizeUpper(header.Rfc);
        if (!string.IsNullOrWhiteSpace(rfc))
        {
            var byRfc = employees.FirstOrDefault(x => NormalizeUpper(x.Rfc) == rfc);
            if (byRfc is not null)
                return byRfc;
        }

        var nss = DigitsOnly(header.Nss);
        if (!string.IsNullOrWhiteSpace(nss))
        {
            var byNss = employees.FirstOrDefault(x => DigitsOnly(x.Nss) == nss);
            if (byNss is not null)
                return byNss;
        }

        var name = NormalizePersonName(header.Nombre);
        if (!string.IsNullOrWhiteSpace(name))
        {
            var byName = employees.FirstOrDefault(x => NormalizePersonName(x.NombreCompleto) == name);
            if (byName is not null)
                return byName;
        }

        return null;
    }

    private static bool ShouldIgnoreSheet(string sheetName, List<List<string?>> rows)
    {
        var normalizedName = NormalizeKey(sheetName);

        if (ContainsAny(normalizedName, "TABLA", "CALC", "FECHAINGRESO", "CATALOGO", "CATALOGOS"))
            return true;

        if (rows.Count < 3)
            return true;

        var joined = NormalizeKey(string.Join(" ", rows.Take(15).SelectMany(x => x).Where(x => !string.IsNullOrWhiteSpace(x))));

        return !ContainsAny(joined, "VACACION", "VACACIONES", "SALDO", "DISFRUT", "DIASDEVAC", "NOMINA", "NOMBRE");
    }

    private static bool LooksLikeEmployeeSheetName(string sheetName)
    {
        var normalized = NormalizeKey(sheetName);

        if (ContainsAny(normalized, "TABLA", "CALC", "FECHA", "INGRESO", "CATALOGO"))
            return false;

        return normalized.Length >= 3;
    }

    private static int? FindColumn(List<string?> row, params string[] tokens)
    {
        for (var i = 0; i < row.Count; i++)
        {
            var normalized = NormalizeKey(row[i]);
            if (tokens.Any(token => normalized.Contains(NormalizeKey(token))))
                return i;
        }

        return null;
    }

    private static string? GetNeighborValue(List<string?> row, int col)
    {
        for (var i = col + 1; i < Math.Min(row.Count, col + 5); i++)
        {
            if (!string.IsNullOrWhiteSpace(row[i]))
                return row[i];
        }

        return null;
    }

    private static string? GetAt(List<string?> row, int? index)
    {
        if (!index.HasValue)
            return null;

        if (index.Value < 0 || index.Value >= row.Count)
            return null;

        return row[index.Value];
    }

    private static decimal? FindLastNumberNearLabel(List<List<string?>> rows, string label)
    {
        decimal? last = null;

        foreach (var row in rows)
        {
            for (var col = 0; col < row.Count; col++)
            {
                if (!NormalizeKey(row[col]).Contains(NormalizeKey(label)))
                    continue;

                for (var i = col + 1; i < Math.Min(row.Count, col + 8); i++)
                {
                    var value = TryParseDecimal(row[i]);
                    if (value.HasValue)
                        last = value.Value;
                }
            }
        }

        return last;
    }

    private static string? ExtractRelevantObservations(List<List<string?>> rows)
    {
        var observations = rows
            .Select(row => string.Join(" ", row.Where(x => !string.IsNullOrWhiteSpace(x))))
            .Where(x => ContainsAny(NormalizeKey(x), "TOMO", "DISFRUT", "VTA", "VENTA", "PERIODO", "SALDO", "PRIMA"))
            .Distinct()
            .TakeLast(12);

        return NormalizeLongText(string.Join(" | ", observations));
    }

    private static decimal? ExtractPaidDaysFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        decimal total = 0m;
        var found = false;

        foreach (Match match in PagoDiasRegex.Matches(text))
        {
            if (TryParseDecimal(match.Groups[2].Value).HasValue)
            {
                total += TryParseDecimal(match.Groups[2].Value)!.Value;
                found = true;
            }
        }

        return found ? total : null;
    }

    private static int CalculateCurrentServiceYear(DateOnly fechaIngreso)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var years = today.Year - fechaIngreso.Year;
        var anniversary = fechaIngreso.AddYears(Math.Max(0, years));

        if (anniversary > today)
            years--;

        return Math.Max(1, years);
    }

    private static string? NormalizeCellValue(object? raw)
    {
        if (raw is null)
            return null;

        if (raw is DateTime dt)
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (raw is double d)
        {
            if (Math.Abs(d - Math.Round(d)) < 0.0000001)
                return ((decimal)d).ToString("0", CultureInfo.InvariantCulture);

            return ((decimal)d).ToString("0.##", CultureInfo.InvariantCulture);
        }

        var value = Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static decimal? TryParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var cleaned = raw.Trim()
            .Replace("½", ".5", StringComparison.Ordinal)
            .Replace(",", ".", StringComparison.Ordinal);

        var match = Regex.Match(cleaned, @"-?\d+(?:\.\d+)?");
        if (!match.Success)
            return null;

        if (decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static bool ContainsAny(string normalizedValue, params string[] tokens)
    {
        return tokens.Any(token => normalizedValue.Contains(NormalizeKey(token)));
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars).ToUpperInvariant();
    }

    private static string NormalizePersonName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ')
            .ToArray();

        return Regex.Replace(new string(chars), @"\s+", " ").Trim();
    }

    private static string? NormalizeNameText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string? NormalizeUpper(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }

    private static string? DigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string? NormalizeLongText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        return normalized.Length <= 1000 ? normalized : normalized[..1000];
    }

    private sealed class SaldoSistemaEmpleado
    {
        public int EmpleadoId { get; set; }

        public decimal Saldo { get; set; }

        public int Periodos { get; set; }
    }

    private sealed class HeaderData
    {
        public string? NumEmpleado { get; set; }
        public string? Nombre { get; set; }
        public string? Rfc { get; set; }
        public string? Nss { get; set; }
        public string? Puesto { get; set; }
        public string? FechaIngreso { get; set; }
    }

    private sealed class VacationTableData
    {
        public int? FilaReferencia { get; set; }
        public decimal? DiasDerecho { get; set; }
        public decimal? DiasDisfrutados { get; set; }
        public decimal? DiasPagados { get; set; }
        public decimal? Saldo { get; set; }
        public string? Observaciones { get; set; }
    }

    private sealed class EmployeeLookupRow
    {
        public int Id { get; set; }
        public string NumEmpleado { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string? ApellidoMaterno { get; set; }
        public string? Rfc { get; set; }
        public string? Nss { get; set; }
        public DateOnly FechaIngreso { get; set; }

        public string NombreCompleto =>
            string.Join(" ", new[]
            {
                Nombres,
                ApellidoPaterno,
                ApellidoMaterno
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
