using ClosedXML.Excel;
using Gv.Rh.Application.DTOs.Empleados.Import;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Gv.Rh.Infrastructure.Services;

public sealed class EmpleadoImportService : IEmpleadoImportService
{
    private readonly RhDbContext _db;
    private readonly IEmpleadoNumberService _empleadoNumberService;

    private static readonly Regex CurpRegex =
        new(@"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$", RegexOptions.Compiled);

    private static readonly Regex RfcRegex =
        new(@"^([A-Z&Ñ]{3,4})\d{6}([A-Z\d]{3})$", RegexOptions.Compiled);

    private static readonly Regex NssRegex =
        new(@"^\d{11}$", RegexOptions.Compiled);

    private static readonly Regex CpRegex =
        new(@"^\d{5}$", RegexOptions.Compiled);

    private static readonly Regex PhoneRegex =
        new(@"^\d{10,15}$", RegexOptions.Compiled);

    private static readonly string[] Headers =
    [
        "NumEmpleado",
        "Nombres",
        "ApellidoPaterno",
        "ApellidoMaterno",
        "FechaNacimiento",
        "FechaIngreso",
        "Telefono",
        "Email",
        "Activo",
        "Curp",
        "Rfc",
        "Nss",
        "Sexo",
        "EstadoCivil",
        "Nacionalidad",
        "DireccionCalle",
        "DireccionNumeroExterior",
        "DireccionNumeroInterior",
        "DireccionColonia",
        "DireccionCiudad",
        "DireccionEstado",
        "DireccionCodigoPostal",
        "CodigoPostalFiscal",
        "EntidadFiscal",
        "ContactoEmergenciaNombre",
        "ContactoEmergenciaTelefono",
        "ContactoEmergenciaParentesco",
        "Departamento",
        "Puesto",
        "Sucursal"
    ];

    public EmpleadoImportService(
        RhDbContext db,
        IEmpleadoNumberService empleadoNumberService)
    {
        _db = db;
        _empleadoNumberService = empleadoNumberService;
    }

    public async Task<byte[]> BuildTemplateAsync(CancellationToken cancellationToken = default)
    {
        var departamentos = await _db.Departamentos
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Nombre)
            .Select(x => x.Nombre)
            .ToListAsync(cancellationToken);

        var puestos = await _db.Puestos
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Nombre)
            .Select(x => x.Nombre)
            .ToListAsync(cancellationToken);

        var sucursales = await _db.Sucursales
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Nombre)
            .Select(x => x.Nombre)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();

        var ws = workbook.Worksheets.Add("Empleados");
        for (var i = 0; i < Headers.Length; i++)
        {
            ws.Cell(1, i + 1).SetValue(Headers[i]);
        }

        var headerRange = ws.Range(1, 1, 1, Headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        var sexoOptions = Enum.GetNames<SexoEmpleado>();
        var estadoCivilOptions = Enum.GetNames<EstadoCivilEmpleado>();

        var example = new Dictionary<string, string?>
        {
            ["NumEmpleado"] = "",
            ["Nombres"] = "JUAN",
            ["ApellidoPaterno"] = "PEREZ",
            ["ApellidoMaterno"] = "LOPEZ",
            ["FechaNacimiento"] = "1990-05-20",
            ["FechaIngreso"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["Telefono"] = "3312345678",
            ["Email"] = "juan.perez@empresa.local",
            ["Activo"] = "SI",
            ["Curp"] = "PELJ900520HJCXXX09",
            ["Rfc"] = "PELJ900520ABC",
            ["Nss"] = "12345678901",
            ["Sexo"] = sexoOptions.FirstOrDefault() ?? "",
            ["EstadoCivil"] = estadoCivilOptions.FirstOrDefault() ?? "",
            ["Nacionalidad"] = "MEXICANA",
            ["DireccionCalle"] = "AV. PATRIA",
            ["DireccionNumeroExterior"] = "1200",
            ["DireccionNumeroInterior"] = "4B",
            ["DireccionColonia"] = "JARDINES",
            ["DireccionCiudad"] = "ZAPOPAN",
            ["DireccionEstado"] = "JALISCO",
            ["DireccionCodigoPostal"] = "45030",
            ["CodigoPostalFiscal"] = "45030",
            ["EntidadFiscal"] = "JALISCO",
            ["ContactoEmergenciaNombre"] = "MARIA PEREZ",
            ["ContactoEmergenciaTelefono"] = "3311122233",
            ["ContactoEmergenciaParentesco"] = "MADRE",
            ["Departamento"] = departamentos.FirstOrDefault() ?? "",
            ["Puesto"] = puestos.FirstOrDefault() ?? "",
            ["Sucursal"] = sucursales.FirstOrDefault() ?? ""
        };

        for (var i = 0; i < Headers.Length; i++)
        {
            var header = Headers[i];
            var value = example.TryGetValue(header, out var exampleValue)
                ? exampleValue ?? string.Empty
                : string.Empty;

            ws.Cell(2, i + 1).SetValue(value);
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        var catalogos = workbook.Worksheets.Add("Catalogos");
        catalogos.Cell(1, 1).SetValue("Departamentos");
        catalogos.Cell(1, 2).SetValue("Puestos");
        catalogos.Cell(1, 3).SetValue("Sucursales");
        catalogos.Cell(1, 4).SetValue("Activo");
        catalogos.Cell(1, 5).SetValue("Sexo");
        catalogos.Cell(1, 6).SetValue("EstadoCivil");

        catalogos.Range(1, 1, 1, 6).Style.Font.Bold = true;
        catalogos.Range(1, 1, 1, 6).Style.Fill.BackgroundColor = XLColor.LightGray;

        WriteColumn(catalogos, 2, 1, departamentos);
        WriteColumn(catalogos, 2, 2, puestos);
        WriteColumn(catalogos, 2, 3, sucursales);
        WriteColumn(catalogos, 2, 4, new[] { "SI", "NO", "TRUE", "FALSE", "1", "0" });
        WriteColumn(catalogos, 2, 5, Enum.GetNames<SexoEmpleado>());
        WriteColumn(catalogos, 2, 6, Enum.GetNames<EstadoCivilEmpleado>());

        catalogos.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<EmpleadoImportValidateResultDto> ValidateAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var analysis = await AnalyzeAsync(stream, cancellationToken);

        return new EmpleadoImportValidateResultDto
        {
            TotalRows = analysis.TotalRows,
            ValidRows = analysis.ValidRows.Count,
            ErrorRows = analysis.Errors
                .Select(x => x.RowNumber)
                .Distinct()
                .Count(),
            Errors = analysis.Errors
                .OrderBy(x => x.RowNumber)
                .ThenBy(x => x.Field)
                .ToList()
        };
    }

    public async Task<EmpleadoImportExecuteResultDto> ImportAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var analysis = await AnalyzeAsync(stream, cancellationToken);

        var inserted = 0;
        var errors = analysis.Errors
            .OrderBy(x => x.RowNumber)
            .ThenBy(x => x.Field)
            .ToList();

        foreach (var row in analysis.ValidRows.OrderBy(x => x.RowNumber))
        {
            try
            {
                var numEmpleado = row.NumEmpleado;
                if (string.IsNullOrWhiteSpace(numEmpleado))
                {
                    numEmpleado = await _empleadoNumberService.GenerateNextAsync(cancellationToken);
                }

                var entity = new Empleado
                {
                    NumEmpleado = numEmpleado!,
                    Nombres = row.Nombres,
                    ApellidoPaterno = row.ApellidoPaterno,
                    ApellidoMaterno = row.ApellidoMaterno,
                    FechaNacimiento = row.FechaNacimiento,
                    Telefono = row.Telefono,
                    Email = row.Email,
                    FechaIngreso = row.FechaIngreso,
                    Activo = row.Activo,
                    EstatusLaboralActual = row.Activo
                        ? EstatusLaboralEmpleado.ACTIVO
                        : EstatusLaboralEmpleado.BAJA,
                    DepartamentoId = row.DepartamentoId,
                    PuestoId = row.PuestoId,
                    SucursalId = row.SucursalId,
                    Curp = row.Curp,
                    Rfc = row.Rfc,
                    Nss = row.Nss,
                    Sexo = row.Sexo,
                    EstadoCivil = row.EstadoCivil,
                    Nacionalidad = row.Nacionalidad,
                    DireccionCalle = row.DireccionCalle,
                    DireccionNumeroExterior = row.DireccionNumeroExterior,
                    DireccionNumeroInterior = row.DireccionNumeroInterior,
                    DireccionColonia = row.DireccionColonia,
                    DireccionCiudad = row.DireccionCiudad,
                    DireccionEstado = row.DireccionEstado,
                    DireccionCodigoPostal = row.DireccionCodigoPostal,
                    CodigoPostalFiscal = row.CodigoPostalFiscal,
                    EntidadFiscal = row.EntidadFiscal,
                    ContactoEmergenciaNombre = row.ContactoEmergenciaNombre,
                    ContactoEmergenciaTelefono = row.ContactoEmergenciaTelefono,
                    ContactoEmergenciaParentesco = row.ContactoEmergenciaParentesco,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                _db.Empleados.Add(entity);
                await _db.SaveChangesAsync(cancellationToken);
                inserted++;
            }
            catch (DbUpdateException ex)
            {
                errors.Add(new EmpleadoImportErrorDto
                {
                    RowNumber = row.RowNumber,
                    Field = "Fila",
                    Message = $"No se pudo guardar la fila. {GetDbErrorMessage(ex)}",
                    Value = null
                });

                _db.ChangeTracker.Clear();
            }
        }

        var errorRows = errors
            .Select(x => x.RowNumber)
            .Distinct()
            .Count();

        return new EmpleadoImportExecuteResultDto
        {
            TotalRows = analysis.TotalRows,
            InsertedRows = inserted,
            SkippedRows = analysis.TotalRows - inserted,
            ErrorRows = errorRows,
            Errors = errors
                .OrderBy(x => x.RowNumber)
                .ThenBy(x => x.Field)
                .ToList()
        };
    }

    private async Task<AnalysisResult> AnalyzeAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is null)
            throw new InvalidDataException("No se recibió un archivo Excel válido.");

        if (stream.CanSeek)
            stream.Position = 0;

        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheets.FirstOrDefault(x =>
                            string.Equals(x.Name, "Empleados", StringComparison.OrdinalIgnoreCase))
                        ?? workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
            throw new InvalidDataException("El archivo Excel no contiene hojas.");

        var headerMap = BuildHeaderMap(worksheet);
        ValidateHeaders(headerMap);

        var rawRows = ReadRows(worksheet, headerMap);
        var errors = new List<EmpleadoImportErrorDto>();

        if (rawRows.Count == 0)
        {
            return new AnalysisResult
            {
                TotalRows = 0,
                Errors = errors,
                ValidRows = new List<PreparedRow>()
            };
        }

        var departamentoEntities = await _db.Departamentos
            .AsNoTracking()
            .Where(x => x.Activo)
            .ToListAsync(cancellationToken);

        var puestoEntities = await _db.Puestos
            .AsNoTracking()
            .Where(x => x.Activo)
            .ToListAsync(cancellationToken);

        var sucursalEntities = await _db.Sucursales
            .AsNoTracking()
            .Where(x => x.Activo)
            .ToListAsync(cancellationToken);

        var departamentosByName = departamentoEntities
            .GroupBy(x => NormalizeLookup(x.Nombre))
            .Where(x => x.Key is not null)
            .ToDictionary(x => x.Key!, x => x.First());

        var puestosByName = puestoEntities
            .GroupBy(x => NormalizeLookup(x.Nombre))
            .Where(x => x.Key is not null)
            .ToDictionary(x => x.Key!, x => x.First());

        var sucursalesByKey = new Dictionary<string, Sucursal>(StringComparer.OrdinalIgnoreCase);
        foreach (var sucursal in sucursalEntities)
        {
            AddLookupIfMissing(sucursalesByKey, sucursal.Nombre, sucursal);
            AddLookupIfMissing(sucursalesByKey, sucursal.Clave, sucursal);
        }

        var numEmpleadoKeys = rawRows
            .Select(x => NormalizeUpperNullable(x.NumEmpleado))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var emailKeys = rawRows
            .Select(x => NormalizeLowerNullable(x.Email))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var curpKeys = rawRows
            .Select(x => NormalizeUpperNullable(x.Curp))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var rfcKeys = rawRows
            .Select(x => NormalizeUpperNullable(x.Rfc))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var nssKeys = rawRows
            .Select(x => DigitsOnly(x.Nss))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var existingNumEmpleado = numEmpleadoKeys.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.Empleados.AsNoTracking()
                .Where(x => numEmpleadoKeys.Contains(x.NumEmpleado))
                .Select(x => x.NumEmpleado)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingEmails = emailKeys.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.Empleados.AsNoTracking()
                .Where(x => x.Email != null && emailKeys.Contains(x.Email))
                .Select(x => x.Email!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingCurps = curpKeys.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.Empleados.AsNoTracking()
                .Where(x => x.Curp != null && curpKeys.Contains(x.Curp))
                .Select(x => x.Curp!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingRfcs = rfcKeys.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.Empleados.AsNoTracking()
                .Where(x => x.Rfc != null && rfcKeys.Contains(x.Rfc))
                .Select(x => x.Rfc!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingNss = nssKeys.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _db.Empleados.AsNoTracking()
                .Where(x => x.Nss != null && nssKeys.Contains(x.Nss))
                .Select(x => x.Nss!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seenNumEmpleado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmail = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCurp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenRfc = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNss = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var validRows = new List<PreparedRow>();

        foreach (var raw in rawRows)
        {
            var rowErrors = new List<EmpleadoImportErrorDto>();

            var nombres = NormalizeNullable(raw.Nombres);
            var apellidoPaterno = NormalizeNullable(raw.ApellidoPaterno);
            var apellidoMaterno = NormalizeNullable(raw.ApellidoMaterno);

            var numEmpleado = NormalizeUpperNullable(raw.NumEmpleado);
            var email = NormalizeLowerNullable(raw.Email);
            var curp = NormalizeUpperNullable(raw.Curp);
            var rfc = NormalizeUpperNullable(raw.Rfc);
            var nss = DigitsOnly(raw.Nss);
            var telefono = DigitsOnly(raw.Telefono);
            var cp = DigitsOnly(raw.DireccionCodigoPostal);
            var cpFiscal = DigitsOnly(raw.CodigoPostalFiscal);
            var telEmergencia = DigitsOnly(raw.ContactoEmergenciaTelefono);

            if (string.IsNullOrWhiteSpace(nombres))
                AddError(rowErrors, raw.RowNumber, "Nombres", "El nombre es obligatorio.", raw.Nombres);

            if (string.IsNullOrWhiteSpace(apellidoPaterno))
                AddError(rowErrors, raw.RowNumber, "ApellidoPaterno", "El apellido paterno es obligatorio.", raw.ApellidoPaterno);

            if (!TryParseDateOnly(raw.FechaIngreso, out var fechaIngreso))
                AddError(rowErrors, raw.RowNumber, "FechaIngreso", "La fecha de ingreso es obligatoria y debe tener un formato válido.", raw.FechaIngreso);

            DateOnly? fechaNacimiento = null;
            if (!string.IsNullOrWhiteSpace(raw.FechaNacimiento))
            {
                if (TryParseDateOnly(raw.FechaNacimiento, out var fechaNac))
                    fechaNacimiento = fechaNac;
                else
                    AddError(rowErrors, raw.RowNumber, "FechaNacimiento", "La fecha de nacimiento no tiene un formato válido.", raw.FechaNacimiento);
            }

            var activo = true;
            if (!string.IsNullOrWhiteSpace(raw.Activo))
            {
                if (!TryParseBool(raw.Activo, out activo))
                    AddError(rowErrors, raw.RowNumber, "Activo", "El valor de Activo debe ser SI/NO, TRUE/FALSE o 1/0.", raw.Activo);
            }

            if (!string.IsNullOrWhiteSpace(curp) && !CurpRegex.IsMatch(curp))
                AddError(rowErrors, raw.RowNumber, "Curp", "La CURP no tiene un formato válido.", raw.Curp);

            if (!string.IsNullOrWhiteSpace(rfc) && !RfcRegex.IsMatch(rfc))
                AddError(rowErrors, raw.RowNumber, "Rfc", "El RFC no tiene un formato válido.", raw.Rfc);

            if (!string.IsNullOrWhiteSpace(nss) && !NssRegex.IsMatch(nss))
                AddError(rowErrors, raw.RowNumber, "Nss", "El NSS debe contener exactamente 11 dígitos.", raw.Nss);

            if (!string.IsNullOrWhiteSpace(telefono) && !PhoneRegex.IsMatch(telefono))
                AddError(rowErrors, raw.RowNumber, "Telefono", "El teléfono debe contener entre 10 y 15 dígitos.", raw.Telefono);

            if (!string.IsNullOrWhiteSpace(cp) && !CpRegex.IsMatch(cp))
                AddError(rowErrors, raw.RowNumber, "DireccionCodigoPostal", "El código postal debe contener exactamente 5 dígitos.", raw.DireccionCodigoPostal);

            if (!string.IsNullOrWhiteSpace(cpFiscal) && !CpRegex.IsMatch(cpFiscal))
                AddError(rowErrors, raw.RowNumber, "CodigoPostalFiscal", "El código postal fiscal debe contener exactamente 5 dígitos.", raw.CodigoPostalFiscal);

            if (!string.IsNullOrWhiteSpace(telEmergencia) && !PhoneRegex.IsMatch(telEmergencia))
                AddError(rowErrors, raw.RowNumber, "ContactoEmergenciaTelefono", "El teléfono de emergencia debe contener entre 10 y 15 dígitos.", raw.ContactoEmergenciaTelefono);

            SexoEmpleado sexo = default;
            if (!TryParseRequiredEnum(raw.Sexo, out sexo))
            {
                AddError(
                    rowErrors,
                    raw.RowNumber,
                    "Sexo",
                    $"El valor de Sexo es obligatorio y debe ser uno de: {string.Join(", ", Enum.GetNames<SexoEmpleado>())}.",
                    raw.Sexo);
            }

            EstadoCivilEmpleado estadoCivil = default;
            if (!TryParseRequiredEnum(raw.EstadoCivil, out estadoCivil))
            {
                AddError(
                    rowErrors,
                    raw.RowNumber,
                    "EstadoCivil",
                    $"El valor de EstadoCivil es obligatorio y debe ser uno de: {string.Join(", ", Enum.GetNames<EstadoCivilEmpleado>())}.",
                    raw.EstadoCivil);
            }

            int? departamentoId = null;
            int? puestoId = null;
            int? sucursalId = null;

            Departamento? departamento = null;
            Puesto? puesto = null;
            Sucursal? sucursal = null;

            var departamentoKey = NormalizeLookup(raw.Departamento);
            var puestoKey = NormalizeLookup(raw.Puesto);
            var sucursalKey = NormalizeLookup(raw.Sucursal);

            if (!string.IsNullOrWhiteSpace(departamentoKey))
            {
                if (!departamentosByName.TryGetValue(departamentoKey, out departamento))
                {
                    AddError(rowErrors, raw.RowNumber, "Departamento", "El departamento no existe o está inactivo.", raw.Departamento);
                }
                else
                {
                    departamentoId = departamento.Id;
                }
            }

            if (!string.IsNullOrWhiteSpace(puestoKey))
            {
                if (!puestosByName.TryGetValue(puestoKey, out puesto))
                {
                    AddError(rowErrors, raw.RowNumber, "Puesto", "El puesto no existe o está inactivo.", raw.Puesto);
                }
                else
                {
                    puestoId = puesto.Id;

                    if (departamentoId.HasValue && puesto.DepartamentoId != departamentoId.Value)
                    {
                        AddError(rowErrors, raw.RowNumber, "Puesto", "El puesto no pertenece al departamento indicado.", raw.Puesto);
                    }
                    else if (!departamentoId.HasValue)
                    {
                        departamentoId = puesto.DepartamentoId;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(sucursalKey))
            {
                if (!sucursalesByKey.TryGetValue(sucursalKey, out sucursal))
                {
                    AddError(rowErrors, raw.RowNumber, "Sucursal", "La sucursal no existe o está inactiva.", raw.Sucursal);
                }
                else
                {
                    sucursalId = sucursal.Id;
                }
            }

            ValidateDuplicateKey(
                rowErrors, raw.RowNumber, "NumEmpleado", numEmpleado, raw.NumEmpleado,
                seenNumEmpleado, existingNumEmpleado,
                "El número de empleado está duplicado dentro del archivo.",
                "Ya existe un empleado con ese número.");

            ValidateDuplicateKey(
                rowErrors, raw.RowNumber, "Email", email, raw.Email,
                seenEmail, existingEmails,
                "El email está duplicado dentro del archivo.",
                "Ya existe un empleado con ese email.");

            ValidateDuplicateKey(
                rowErrors, raw.RowNumber, "Curp", curp, raw.Curp,
                seenCurp, existingCurps,
                "La CURP está duplicada dentro del archivo.",
                "La CURP ya está registrada en otro empleado.");

            ValidateDuplicateKey(
                rowErrors, raw.RowNumber, "Rfc", rfc, raw.Rfc,
                seenRfc, existingRfcs,
                "El RFC está duplicado dentro del archivo.",
                "El RFC ya está registrado en otro empleado.");

            ValidateDuplicateKey(
                rowErrors, raw.RowNumber, "Nss", nss, raw.Nss,
                seenNss, existingNss,
                "El NSS está duplicado dentro del archivo.",
                "El NSS ya está registrado en otro empleado.");

            if (rowErrors.Count == 0)
            {
                validRows.Add(new PreparedRow
                {
                    RowNumber = raw.RowNumber,
                    NumEmpleado = numEmpleado,
                    Nombres = nombres!,
                    ApellidoPaterno = apellidoPaterno!,
                    ApellidoMaterno = apellidoMaterno,
                    FechaNacimiento = fechaNacimiento,
                    FechaIngreso = fechaIngreso!.Value,
                    Telefono = telefono,
                    Email = email,
                    Activo = activo,
                    Curp = curp,
                    Rfc = rfc,
                    Nss = nss,
                    Sexo = sexo,
                    EstadoCivil = estadoCivil,
                    Nacionalidad = NormalizeNullable(raw.Nacionalidad),
                    DireccionCalle = NormalizeNullable(raw.DireccionCalle),
                    DireccionNumeroExterior = NormalizeNullable(raw.DireccionNumeroExterior),
                    DireccionNumeroInterior = NormalizeNullable(raw.DireccionNumeroInterior),
                    DireccionColonia = NormalizeNullable(raw.DireccionColonia),
                    DireccionCiudad = NormalizeNullable(raw.DireccionCiudad),
                    DireccionEstado = NormalizeNullable(raw.DireccionEstado),
                    DireccionCodigoPostal = cp,
                    CodigoPostalFiscal = cpFiscal,
                    EntidadFiscal = NormalizeNullable(raw.EntidadFiscal),
                    ContactoEmergenciaNombre = NormalizeNullable(raw.ContactoEmergenciaNombre),
                    ContactoEmergenciaTelefono = telEmergencia,
                    ContactoEmergenciaParentesco = NormalizeNullable(raw.ContactoEmergenciaParentesco),
                    DepartamentoId = departamentoId,
                    PuestoId = puestoId,
                    SucursalId = sucursalId
                });
            }

            errors.AddRange(rowErrors);
        }

        return new AnalysisResult
        {
            TotalRows = rawRows.Count,
            Errors = errors,
            ValidRows = validRows
        };
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLWorksheet worksheet)
    {
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var col = 1; col <= lastColumn; col++)
        {
            var header = worksheet.Cell(1, col).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
            {
                map[header] = col;
            }
        }

        return map;
    }

    private static void ValidateHeaders(Dictionary<string, int> headerMap)
    {
        var missing = Headers
            .Where(x => !headerMap.ContainsKey(x))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidDataException(
                "El archivo no contiene los encabezados esperados. Faltan: " + string.Join(", ", missing));
        }
    }

    private static List<RawRow> ReadRows(IXLWorksheet worksheet, Dictionary<string, int> headerMap)
    {
        var rows = new List<RawRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (IsRowEmpty(row, headerMap))
                continue;

            rows.Add(new RawRow
            {
                RowNumber = rowNumber,
                NumEmpleado = GetCellValue(row, headerMap, "NumEmpleado"),
                Nombres = GetCellValue(row, headerMap, "Nombres"),
                ApellidoPaterno = GetCellValue(row, headerMap, "ApellidoPaterno"),
                ApellidoMaterno = GetCellValue(row, headerMap, "ApellidoMaterno"),
                FechaNacimiento = GetCellValue(row, headerMap, "FechaNacimiento"),
                FechaIngreso = GetCellValue(row, headerMap, "FechaIngreso"),
                Telefono = GetCellValue(row, headerMap, "Telefono"),
                Email = GetCellValue(row, headerMap, "Email"),
                Activo = GetCellValue(row, headerMap, "Activo"),
                Curp = GetCellValue(row, headerMap, "Curp"),
                Rfc = GetCellValue(row, headerMap, "Rfc"),
                Nss = GetCellValue(row, headerMap, "Nss"),
                Sexo = GetCellValue(row, headerMap, "Sexo"),
                EstadoCivil = GetCellValue(row, headerMap, "EstadoCivil"),
                Nacionalidad = GetCellValue(row, headerMap, "Nacionalidad"),
                DireccionCalle = GetCellValue(row, headerMap, "DireccionCalle"),
                DireccionNumeroExterior = GetCellValue(row, headerMap, "DireccionNumeroExterior"),
                DireccionNumeroInterior = GetCellValue(row, headerMap, "DireccionNumeroInterior"),
                DireccionColonia = GetCellValue(row, headerMap, "DireccionColonia"),
                DireccionCiudad = GetCellValue(row, headerMap, "DireccionCiudad"),
                DireccionEstado = GetCellValue(row, headerMap, "DireccionEstado"),
                DireccionCodigoPostal = GetCellValue(row, headerMap, "DireccionCodigoPostal"),
                CodigoPostalFiscal = GetCellValue(row, headerMap, "CodigoPostalFiscal"),
                EntidadFiscal = GetCellValue(row, headerMap, "EntidadFiscal"),
                ContactoEmergenciaNombre = GetCellValue(row, headerMap, "ContactoEmergenciaNombre"),
                ContactoEmergenciaTelefono = GetCellValue(row, headerMap, "ContactoEmergenciaTelefono"),
                ContactoEmergenciaParentesco = GetCellValue(row, headerMap, "ContactoEmergenciaParentesco"),
                Departamento = GetCellValue(row, headerMap, "Departamento"),
                Puesto = GetCellValue(row, headerMap, "Puesto"),
                Sucursal = GetCellValue(row, headerMap, "Sucursal")
            });
        }

        return rows;
    }

    private static bool IsRowEmpty(IXLRow row, Dictionary<string, int> headerMap)
    {
        foreach (var column in headerMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(GetCellValue(row, column)))
                return false;
        }

        return true;
    }

    private static string? GetCellValue(IXLRow row, Dictionary<string, int> headerMap, string header)
    {
        return headerMap.TryGetValue(header, out var column)
            ? GetCellValue(row, column)
            : null;
    }

    private static string? GetCellValue(IXLRow row, int column)
    {
        var cell = row.Cell(column);

        if (cell.IsEmpty())
            return null;

        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().ToString("yyyy-MM-dd");

        var value = cell.GetFormattedString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void AddLookupIfMissing(
        Dictionary<string, Sucursal> dict,
        string? rawKey,
        Sucursal entity)
    {
        var key = NormalizeLookup(rawKey);
        if (!string.IsNullOrWhiteSpace(key) && !dict.ContainsKey(key))
            dict[key] = entity;
    }

    private static string? NormalizeLookup(string? value)
    {
        return NormalizeUpperNullable(value);
    }

    private static void WriteColumn(IXLWorksheet ws, int startRow, int col, IEnumerable<string> values)
    {
        var row = startRow;
        foreach (var item in values)
        {
            ws.Cell(row++, col).SetValue(item);
        }
    }

    private static void AddError(
        List<EmpleadoImportErrorDto> errors,
        int rowNumber,
        string field,
        string message,
        string? value)
    {
        errors.Add(new EmpleadoImportErrorDto
        {
            RowNumber = rowNumber,
            Field = field,
            Message = message,
            Value = value
        });
    }

    private static void ValidateDuplicateKey(
        List<EmpleadoImportErrorDto> errors,
        int rowNumber,
        string field,
        string? normalizedValue,
        string? originalValue,
        HashSet<string> seenSet,
        HashSet<string> existingSet,
        string duplicateInFileMessage,
        string duplicateInDbMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedValue))
            return;

        if (!seenSet.Add(normalizedValue))
        {
            AddError(errors, rowNumber, field, duplicateInFileMessage, originalValue);
            return;
        }

        if (existingSet.Contains(normalizedValue))
        {
            AddError(errors, rowNumber, field, duplicateInDbMessage, originalValue);
        }
    }

    private static bool TryParseDateOnly(string? raw, out DateOnly? value)
    {
        value = null;

        var normalized = NormalizeNullable(raw);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var formats = new[]
        {
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy",
            "MM/dd/yyyy",
            "M/d/yyyy"
        };

        if (DateTime.TryParseExact(
                normalized,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactDate))
        {
            value = DateOnly.FromDateTime(exactDate);
            return true;
        }

        if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var genericDate))
        {
            value = DateOnly.FromDateTime(genericDate);
            return true;
        }

        if (DateTime.TryParse(normalized, new CultureInfo("es-MX"), DateTimeStyles.None, out var mxDate))
        {
            value = DateOnly.FromDateTime(mxDate);
            return true;
        }

        return false;
    }

    private static bool TryParseBool(string? raw, out bool value)
    {
        value = false;

        var normalized = NormalizeUpperNullable(raw);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        switch (normalized)
        {
            case "SI":
            case "SÍ":
            case "TRUE":
            case "1":
            case "ACTIVO":
                value = true;
                return true;

            case "NO":
            case "FALSE":
            case "0":
            case "INACTIVO":
                value = false;
                return true;

            default:
                return false;
        }
    }

    private static bool TryParseRequiredEnum<TEnum>(string? raw, out TEnum value)
    where TEnum : struct, Enum
{
    value = default;

    var normalized = NormalizeNullable(raw);
    if (string.IsNullOrWhiteSpace(normalized))
        return false;

    var candidate = NormalizeEnumToken(normalized);

    if (Enum.TryParse<TEnum>(candidate, ignoreCase: true, out var parsed))
    {
        value = parsed;
        return true;
    }

    return false;
}

    private static string NormalizeEnumToken(string value)
    {
        var normalized = value.Trim().Replace("-", "_").Replace(" ", "_");
        normalized = RemoveDiacritics(normalized);
        return normalized.ToUpperInvariant();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray();

        return new string(chars).Normalize(NormalizationForm.FormC);
    }

    private static string? NormalizeNullable(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeUpperNullable(string? value)
    {
        var normalized = NormalizeNullable(value);
        return normalized?.ToUpperInvariant();
    }

    private static string? NormalizeLowerNullable(string? value)
    {
        var normalized = NormalizeNullable(value);
        return normalized?.ToLowerInvariant();
    }

    private static string? DigitsOnly(string? value)
    {
        var normalized = NormalizeNullable(value);
        if (normalized is null)
            return null;

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string GetDbErrorMessage(DbUpdateException ex)
    {
        var inner = ex.InnerException?.Message;
        if (!string.IsNullOrWhiteSpace(inner))
            return inner;

        return ex.Message;
    }

    private sealed class AnalysisResult
    {
        public int TotalRows { get; set; }
        public List<PreparedRow> ValidRows { get; set; } = [];
        public List<EmpleadoImportErrorDto> Errors { get; set; } = [];
    }

    private sealed class RawRow
    {
        public int RowNumber { get; set; }
        public string? NumEmpleado { get; set; }
        public string? Nombres { get; set; }
        public string? ApellidoPaterno { get; set; }
        public string? ApellidoMaterno { get; set; }
        public string? FechaNacimiento { get; set; }
        public string? FechaIngreso { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Activo { get; set; }
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
        public string? EntidadFiscal { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
        public string? ContactoEmergenciaParentesco { get; set; }
        public string? Departamento { get; set; }
        public string? Puesto { get; set; }
        public string? Sucursal { get; set; }
    }

    private sealed class PreparedRow
    {
        public int RowNumber { get; set; }
        public string? NumEmpleado { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string? ApellidoMaterno { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public DateOnly FechaIngreso { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public bool Activo { get; set; }
        public string? Curp { get; set; }
        public string? Rfc { get; set; }
        public string? Nss { get; set; }
        public SexoEmpleado Sexo { get; set; }
        public EstadoCivilEmpleado EstadoCivil { get; set; }
        public string? Nacionalidad { get; set; }
        public string? DireccionCalle { get; set; }
        public string? DireccionNumeroExterior { get; set; }
        public string? DireccionNumeroInterior { get; set; }
        public string? DireccionColonia { get; set; }
        public string? DireccionCiudad { get; set; }
        public string? DireccionEstado { get; set; }
        public string? DireccionCodigoPostal { get; set; }
        public string? CodigoPostalFiscal { get; set; }
        public string? EntidadFiscal { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
        public string? ContactoEmergenciaParentesco { get; set; }
        public int? DepartamentoId { get; set; }
        public int? PuestoId { get; set; }
        public int? SucursalId { get; set; }
    }
}

