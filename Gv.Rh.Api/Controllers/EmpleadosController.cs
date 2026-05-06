using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Empleados;
using Gv.Rh.Application.DTOs.Empleados.Import;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmpleadosController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IEmpleadosReportService _empleadosReportService;
    private readonly IEmpleadoFichaReportService _empleadoFichaReportService;
    private readonly IEmpleadoNumberService _empleadoNumberService;
    private readonly IEmpleadoImportService _empleadoImportService;
    private readonly IEmpleadoMovimientoLaboralService _empleadoMovimientoLaboralService;

    /// <summary>
    /// Servicio de vacaciones.
    /// Se usa desde baja/DELETE para cerrar periodos vacacionales abiertos
    /// sin borrar historial, sin descontar saldo y dejando evidencia en kárdex.
    /// </summary>
    private readonly IVacacionesService _vacacionesService;

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

    private const long MaxImportFileBytes = 10 * 1024 * 1024; // 10 MB
    private readonly IEmpleadoAccessScopeService _empleadoAccessScopeService;


    public EmpleadosController(
        RhDbContext db,
        IWebHostEnvironment env,
        IEmpleadosReportService empleadosReportService,
        IEmpleadoFichaReportService empleadoFichaReportService,
        IEmpleadoNumberService empleadoNumberService,
        IEmpleadoImportService empleadoImportService,
        IEmpleadoMovimientoLaboralService empleadoMovimientoLaboralService,
        IVacacionesService vacacionesService,
        IEmpleadoAccessScopeService empleadoAccessScopeService)
    {
        _db = db;
                _empleadoAccessScopeService = empleadoAccessScopeService;
_env = env;
        _empleadosReportService = empleadosReportService;
        _empleadoFichaReportService = empleadoFichaReportService;
        _empleadoNumberService = empleadoNumberService;
        _empleadoImportService = empleadoImportService;
        _empleadoMovimientoLaboralService = empleadoMovimientoLaboralService;
        _vacacionesService = vacacionesService;
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] bool? activo = null,
        [FromQuery] int? departamentoId = null,
        [FromQuery] int? puestoId = null,
        [FromQuery] int? sucursalId = null,
        [FromQuery] string sort = "id",
        [FromQuery] string dir = "desc")
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<Empleado> query = _db.Empleados
            .AsNoTracking()
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Include(x => x.Sucursal)
            .Include(x => x.AprobadorPrimario)
            .Include(x => x.AprobadorSecundario);

        if (activo.HasValue)
            query = query.Where(e => e.Activo == activo.Value);

        if (departamentoId.HasValue)
            query = query.Where(e => e.DepartamentoId == departamentoId.Value);

        if (puestoId.HasValue)
            query = query.Where(e => e.PuestoId == puestoId.Value);

        if (sucursalId.HasValue)
            query = query.Where(e => e.SucursalId == sucursalId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();

            query = query.Where(e =>
                EF.Functions.ILike(e.NumEmpleado, $"%{q}%") ||
                EF.Functions.ILike(e.Nombres, $"%{q}%") ||
                EF.Functions.ILike(e.ApellidoPaterno, $"%{q}%") ||
                (e.ApellidoMaterno != null && EF.Functions.ILike(e.ApellidoMaterno, $"%{q}%")) ||
                (e.Email != null && EF.Functions.ILike(e.Email, $"%{q}%")) ||
                (e.Curp != null && EF.Functions.ILike(e.Curp, $"%{q}%")) ||
                (e.Rfc != null && EF.Functions.ILike(e.Rfc, $"%{q}%")) ||
                (e.Nss != null && EF.Functions.ILike(e.Nss, $"%{q}%")) ||
                (e.Departamento != null && EF.Functions.ILike(e.Departamento.Nombre, $"%{q}%")) ||
                (e.Puesto != null && EF.Functions.ILike(e.Puesto.Nombre, $"%{q}%")) ||
                (e.Sucursal != null && EF.Functions.ILike(e.Sucursal.Nombre, $"%{q}%")) ||
                (e.Sucursal != null && EF.Functions.ILike(e.Sucursal.Clave, $"%{q}%")) ||
                (e.AprobadorPrimario != null && EF.Functions.ILike(e.AprobadorPrimario.Nombres, $"%{q}%")) ||
                (e.AprobadorPrimario != null && EF.Functions.ILike(e.AprobadorPrimario.ApellidoPaterno, $"%{q}%")) ||
                (e.AprobadorSecundario != null && EF.Functions.ILike(e.AprobadorSecundario.Nombres, $"%{q}%")) ||
                (e.AprobadorSecundario != null && EF.Functions.ILike(e.AprobadorSecundario.ApellidoPaterno, $"%{q}%"))
            );
        }

        var asc = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);

        query = (sort?.ToLowerInvariant()) switch
        {
            "numempleado" => asc ? query.OrderBy(x => x.NumEmpleado) : query.OrderByDescending(x => x.NumEmpleado),
            "nombre" or "nombres" => asc ? query.OrderBy(x => x.Nombres) : query.OrderByDescending(x => x.Nombres),
            "apellido" or "apellidopaterno" => asc ? query.OrderBy(x => x.ApellidoPaterno) : query.OrderByDescending(x => x.ApellidoPaterno),
            "fechaingreso" => asc ? query.OrderBy(x => x.FechaIngreso) : query.OrderByDescending(x => x.FechaIngreso),
            "departamento" => asc
                ? query.OrderBy(x => x.Departamento != null ? x.Departamento.Nombre : string.Empty)
                : query.OrderByDescending(x => x.Departamento != null ? x.Departamento.Nombre : string.Empty),
            "puesto" => asc
                ? query.OrderBy(x => x.Puesto != null ? x.Puesto.Nombre : string.Empty)
                : query.OrderByDescending(x => x.Puesto != null ? x.Puesto.Nombre : string.Empty),
            "sucursal" => asc
                ? query.OrderBy(x => x.Sucursal != null ? x.Sucursal.Nombre : string.Empty)
                : query.OrderByDescending(x => x.Sucursal != null ? x.Sucursal.Nombre : string.Empty),
            "activo" => asc ? query.OrderBy(x => x.Activo) : query.OrderByDescending(x => x.Activo),
            _ => asc ? query.OrderBy(x => x.Id) : query.OrderByDescending(x => x.Id),
        };

        var total = await query.CountAsync();

        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(HttpContext.RequestAborted);

        var usersByEmpleadoId = await LoadUsersByEmpleadoIdsAsync(entities.Select(x => x.Id).ToList());

        var items = entities
            .Select(entity =>
            {
                var dto = MapEmpleadoDto(entity);
                ApplyLinkedUserInfo(dto, usersByEmpleadoId.TryGetValue(entity.Id, out var user) ? user : null);
                return dto;
            })
            .ToList();

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            items
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var accessError = await EnsureCanAccessEmpleadoAsync(id);
        if (accessError is not null)
            return accessError;

        var dto = await LoadEmpleadoDtoAsync(id);
        return dto is null
            ? NotFound(new { message = "Empleado no encontrado." })
            : Ok(dto);
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/foto")]
    public async Task<IActionResult> GetFoto(int id)
    {
        var empleado = await _db.Empleados
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.FotoRutaRelativa,
                x.FotoMimeType
            })
            .FirstOrDefaultAsync();

        if (empleado is null || string.IsNullOrWhiteSpace(empleado.FotoRutaRelativa))
            return NotFound();

        var webRoot = EnsureWebRootPath();
        var physicalPath = Path.Combine(
            webRoot,
            empleado.FotoRutaRelativa.Replace('/', Path.DirectorySeparatorChar)
        );

        if (!System.IO.File.Exists(physicalPath))
            return NotFound();

        var contentType = string.IsNullOrWhiteSpace(empleado.FotoMimeType)
            ? "application/octet-stream"
            : empleado.FotoMimeType;

        return PhysicalFile(physicalPath, contentType);
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] EmpleadosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _empleadosReportService.BuildXlsxAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] EmpleadosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _empleadosReportService.BuildPdfAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpGet("{id:int}/ficha/pdf")]
    public async Task<IActionResult> ExportFichaPdf(
        int id,
        CancellationToken cancellationToken)
    {
        var report = await _empleadoFichaReportService.BuildPdfAsync(id, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpGet("import/template")]
    [Authorize(Roles = "ADMIN,RRHH")]
    public async Task<IActionResult> DownloadImportTemplate(CancellationToken cancellationToken)
    {
        var content = await _empleadoImportService.BuildTemplateAsync(cancellationToken);

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "empleados_import_template.xlsx");
    }

    [HttpPost("import/validate")]
    [Authorize(Roles = "ADMIN,RRHH")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<EmpleadoImportValidateResultDto>> ValidateImport(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var fileError = ValidateImportFile(file);
        if (fileError is not null)
            return fileError;

        await using var stream = file.OpenReadStream();

        try
        {
            var result = await _empleadoImportService.ValidateAsync(stream, cancellationToken);
            return Ok(result);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("import")]
    [Authorize(Roles = "ADMIN,RRHH")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<EmpleadoImportExecuteResultDto>> Import(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var fileError = ValidateImportFile(file);
        if (fileError is not null)
            return fileError;

        await using var stream = file.OpenReadStream();

        try
        {
            var result = await _empleadoImportService.ImportAsync(stream, cancellationToken);
            return Ok(result);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost("{id:int}/create-account")]
    public async Task<IActionResult> CreateAccount(int id, [FromBody] CreateAccountForEmpleadoDto dto)
    {
        var empleadoExists = await _db.Empleados.AsNoTracking().AnyAsync(x => x.Id == id, HttpContext.RequestAborted);
        if (!empleadoExists)
            return NotFound(new { message = "Empleado no existe." });

        var alreadyLinked = await _db.Users.AnyAsync(u => u.EmpleadoId == id);
        if (alreadyLinked)
            return Conflict(new { message = "Este empleado ya tiene cuenta." });

        var email = dto.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email es requerido." });

        var emailExists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (emailExists)
            return Conflict(new { message = "Email ya existe." });

        var role = UserRoles.Normalize(dto.Role);
        if (!UserRoles.IsValid(role))
            return BadRequest(new { message = $"Rol inválido. Permitidos: {string.Join(", ", UserRoles.All)}" });

        var password = dto.Password.Trim();
        if (string.IsNullOrWhiteSpace(password))
            return BadRequest(new { message = "Password es requerido." });

        var user = new AppUser
        {
            Email = email,
            Role = role,
            IsActive = dto.IsActive,
            EmpleadoId = id,
            MustChangePassword = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Cuenta creada y ligada al empleado.",
            user = new
            {
                user.Id,
                user.Email,
                user.Role,
                user.IsActive,
                user.MustChangePassword,
                user.EmpleadoId
            }
        });
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost("{id:int}/link-user-by-email")]
    public async Task<IActionResult> LinkUserByEmail(int id)
    {
        var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.Id == id);
        if (empleado is null)
            return NotFound(new { message = "Empleado no existe." });

        var empEmail = empleado.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(empEmail))
            return BadRequest(new { message = "El empleado no tiene Email capturado." });

        var alreadyLinkedToEmpleado = await _db.Users.AnyAsync(u => u.EmpleadoId == id);
        if (alreadyLinkedToEmpleado)
            return Conflict(new { message = "Este empleado ya tiene cuenta ligada." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == empEmail);
        if (user is null)
            return NotFound(new { message = "No existe usuario con el email del empleado." });

        if (user.EmpleadoId.HasValue && user.EmpleadoId.Value != id)
            return Conflict(new { message = "Ese usuario ya está ligado a otro empleado." });

        user.EmpleadoId = id;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Usuario ligado al empleado por email.",
            user = new
            {
                user.Id,
                user.Email,
                user.Role,
                user.IsActive,
                user.MustChangePassword,
                user.EmpleadoId
            },
            empleado = new
            {
                empleado.Id,
                empleado.NumEmpleado,
                empleado.Nombres,
                empleado.ApellidoPaterno,
                empleado.ApellidoMaterno,
                empleado.Email
            }
        });
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EmpleadoCreateDto dto)
    {
        var relationResult = await ResolveRelationsAsync(dto.DepartamentoId, dto.PuestoId, dto.SucursalId);
        if (relationResult.Error is not null)
            return relationResult.Error;

        var aprobadoresResult = await ResolveAprobadoresAsync(
            dto.AprobadorPrimarioEmpleadoId,
            dto.AprobadorSecundarioEmpleadoId,
            null);

        if (aprobadoresResult.Error is not null)
            return aprobadoresResult.Error;

        var validationError = ValidateEmpleadoExtendedFields(
            dto.Curp,
            dto.Rfc,
            dto.Nss,
            dto.Telefono,
            dto.DireccionCodigoPostal,
            dto.CodigoPostalFiscal,
            dto.ContactoEmergenciaTelefono);

        if (validationError is not null)
            return validationError;

        var normalizedCurp = NormalizeUpperNullable(dto.Curp);
        var normalizedRfc = NormalizeUpperNullable(dto.Rfc);
        var normalizedNss = DigitsOnly(dto.Nss);
        var normalizedTelefono = DigitsOnly(dto.Telefono);
        var normalizedCp = DigitsOnly(dto.DireccionCodigoPostal);
        var normalizedCpFiscal = DigitsOnly(dto.CodigoPostalFiscal);
        var normalizedContactoTelefono = DigitsOnly(dto.ContactoEmergenciaTelefono);

        var duplicatedIdentityError = await ValidateDuplicateIdentityAsync(
            null,
            normalizedCurp,
            normalizedRfc,
            normalizedNss);

        if (duplicatedIdentityError is not null)
            return duplicatedIdentityError;

        var numEmpleado = NormalizeUpperNullable(dto.NumEmpleado);

        if (!string.IsNullOrWhiteSpace(numEmpleado))
        {
            var existeNumEmpleado = await _db.Empleados.AsNoTracking()
                .AnyAsync(x => x.NumEmpleado == numEmpleado);

            if (existeNumEmpleado)
                return Conflict(new { message = "Ya existe un empleado con ese número." });
        }
        else
        {
            numEmpleado = await _empleadoNumberService.GenerateNextAsync();
        }

        var entity = new Empleado
        {
            NumEmpleado = numEmpleado!,
            Nombres = dto.Nombres.Trim(),
            ApellidoPaterno = dto.ApellidoPaterno.Trim(),
            ApellidoMaterno = NormalizeNullable(dto.ApellidoMaterno),
            FechaNacimiento = dto.FechaNacimiento,
            Telefono = normalizedTelefono,
            Email = NormalizeLowerNullable(dto.Email),
            FechaIngreso = dto.FechaIngreso,
            Activo = dto.Activo,
            EstatusLaboralActual = dto.Activo ? EstatusLaboralEmpleado.ACTIVO : EstatusLaboralEmpleado.BAJA,
            DepartamentoId = relationResult.DepartamentoId,
            PuestoId = dto.PuestoId,
            SucursalId = relationResult.SucursalId,
            AprobadorPrimarioEmpleadoId = aprobadoresResult.AprobadorPrimarioEmpleadoId,
            AprobadorSecundarioEmpleadoId = aprobadoresResult.AprobadorSecundarioEmpleadoId,
            Curp = normalizedCurp,
            Rfc = normalizedRfc,
            Nss = normalizedNss,
            Sexo = dto.Sexo,
            EstadoCivil = dto.EstadoCivil,
            Nacionalidad = NormalizeNullable(dto.Nacionalidad),
            DireccionCalle = NormalizeNullable(dto.DireccionCalle),
            DireccionNumeroExterior = NormalizeNullable(dto.DireccionNumeroExterior),
            DireccionNumeroInterior = NormalizeNullable(dto.DireccionNumeroInterior),
            DireccionColonia = NormalizeNullable(dto.DireccionColonia),
            DireccionCiudad = NormalizeNullable(dto.DireccionCiudad),
            DireccionEstado = NormalizeNullable(dto.DireccionEstado),
            DireccionCodigoPostal = normalizedCp,
            CodigoPostalFiscal = normalizedCpFiscal,
            EntidadFiscal = NormalizeNullable(dto.EntidadFiscal),
            ContactoEmergenciaNombre = NormalizeNullable(dto.ContactoEmergenciaNombre),
            ContactoEmergenciaTelefono = normalizedContactoTelefono,
            ContactoEmergenciaParentesco = NormalizeNullable(dto.ContactoEmergenciaParentesco),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await using var tx = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);

        _db.Empleados.Add(entity);
        await _db.SaveChangesAsync(HttpContext.RequestAborted);

        await _empleadoMovimientoLaboralService.RegistrarAltaAsync(
            entity,
            TryGetCurrentUserId(),
            HttpContext.RequestAborted);

        await tx.CommitAsync(HttpContext.RequestAborted);

        var created = await LoadEmpleadoDtoAsync(entity.Id);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, created);
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] EmpleadoUpdateDto dto)
    {
        var entity = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound(new { message = "Empleado no encontrado." });

        if (dto.Activo != entity.Activo)
        {
            return BadRequest(new
            {
                message = "Para cambiar el estado laboral del empleado use los endpoints de baja o reingreso."
            });
        }

        var relationResult = await ResolveRelationsAsync(dto.DepartamentoId, dto.PuestoId, dto.SucursalId);
        if (relationResult.Error is not null)
            return relationResult.Error;

        var aprobadoresResult = await ResolveAprobadoresAsync(
            dto.AprobadorPrimarioEmpleadoId,
            dto.AprobadorSecundarioEmpleadoId,
            id);

        if (aprobadoresResult.Error is not null)
            return aprobadoresResult.Error;

        var validationError = ValidateEmpleadoExtendedFields(
            dto.Curp,
            dto.Rfc,
            dto.Nss,
            dto.Telefono,
            dto.DireccionCodigoPostal,
            dto.CodigoPostalFiscal,
            dto.ContactoEmergenciaTelefono);

        if (validationError is not null)
            return validationError;

        var normalizedCurp = NormalizeUpperNullable(dto.Curp);
        var normalizedRfc = NormalizeUpperNullable(dto.Rfc);
        var normalizedNss = DigitsOnly(dto.Nss);
        var normalizedTelefono = DigitsOnly(dto.Telefono);
        var normalizedCp = DigitsOnly(dto.DireccionCodigoPostal);
        var normalizedCpFiscal = DigitsOnly(dto.CodigoPostalFiscal);
        var normalizedContactoTelefono = DigitsOnly(dto.ContactoEmergenciaTelefono);

        var duplicatedIdentityError = await ValidateDuplicateIdentityAsync(
            entity.Id,
            normalizedCurp,
            normalizedRfc,
            normalizedNss);

        if (duplicatedIdentityError is not null)
            return duplicatedIdentityError;

        var snapshot = new EmpleadoLaboralSnapshot(
            entity.Id,
            entity.PuestoId,
            entity.DepartamentoId,
            entity.SucursalId,
            entity.EstatusLaboralActual,
            entity.Activo);

        await using var tx = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);

        entity.Nombres = dto.Nombres.Trim();
        entity.ApellidoPaterno = dto.ApellidoPaterno.Trim();
        entity.ApellidoMaterno = NormalizeNullable(dto.ApellidoMaterno);
        entity.FechaNacimiento = dto.FechaNacimiento;
        entity.Telefono = normalizedTelefono;
        entity.Email = NormalizeLowerNullable(dto.Email);
        entity.FechaIngreso = dto.FechaIngreso;
        entity.Activo = dto.Activo;
        entity.EstatusLaboralActual = dto.Activo ? EstatusLaboralEmpleado.ACTIVO : EstatusLaboralEmpleado.BAJA;
        entity.DepartamentoId = relationResult.DepartamentoId;
        entity.PuestoId = dto.PuestoId;
        entity.SucursalId = relationResult.SucursalId;
        entity.AprobadorPrimarioEmpleadoId = aprobadoresResult.AprobadorPrimarioEmpleadoId;
        entity.AprobadorSecundarioEmpleadoId = aprobadoresResult.AprobadorSecundarioEmpleadoId;
        entity.Curp = normalizedCurp;
        entity.Rfc = normalizedRfc;
        entity.Nss = normalizedNss;
        entity.Sexo = dto.Sexo;
        entity.EstadoCivil = dto.EstadoCivil;
        entity.Nacionalidad = NormalizeNullable(dto.Nacionalidad);
        entity.DireccionCalle = NormalizeNullable(dto.DireccionCalle);
        entity.DireccionNumeroExterior = NormalizeNullable(dto.DireccionNumeroExterior);
        entity.DireccionNumeroInterior = NormalizeNullable(dto.DireccionNumeroInterior);
        entity.DireccionColonia = NormalizeNullable(dto.DireccionColonia);
        entity.DireccionCiudad = NormalizeNullable(dto.DireccionCiudad);
        entity.DireccionEstado = NormalizeNullable(dto.DireccionEstado);
        entity.DireccionCodigoPostal = normalizedCp;
        entity.CodigoPostalFiscal = normalizedCpFiscal;
        entity.EntidadFiscal = NormalizeNullable(dto.EntidadFiscal);
        entity.ContactoEmergenciaNombre = NormalizeNullable(dto.ContactoEmergenciaNombre);
        entity.ContactoEmergenciaTelefono = normalizedContactoTelefono;
        entity.ContactoEmergenciaParentesco = NormalizeNullable(dto.ContactoEmergenciaParentesco);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(HttpContext.RequestAborted);

        await _empleadoMovimientoLaboralService.RegistrarCambiosAsync(
            snapshot,
            entity,
            TryGetCurrentUserId(),
            HttpContext.RequestAborted);

        await tx.CommitAsync(HttpContext.RequestAborted);

        var updated = await LoadEmpleadoDtoAsync(entity.Id);
        return Ok(updated);
    }

    [HttpPut("{id:int}/numero-empleado")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> CambiarNumeroEmpleado(int id, [FromBody] CambiarNumeroEmpleadoDto dto)
    {
        var empleado = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (empleado is null)
            return NotFound(new { message = "Empleado no encontrado." });

        var nuevoNumero = NormalizeUpperNullable(dto.NumEmpleadoNuevo);
        if (string.IsNullOrWhiteSpace(nuevoNumero))
            return BadRequest(new { message = "El nuevo número de empleado es obligatorio." });

        if (string.Equals(empleado.NumEmpleado, nuevoNumero, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "El nuevo número de empleado es igual al actual." });

        var repetido = await _db.Empleados.AsNoTracking()
            .AnyAsync(x => x.Id != id && x.NumEmpleado == nuevoNumero);

        if (repetido)
            return Conflict(new { message = "Ya existe otro empleado con ese número." });

        empleado.NumEmpleado = nuevoNumero;
        empleado.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            empleado.Id,
            empleado.NumEmpleado,
            Motivo = NormalizeNullable(dto.Motivo)
        });
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpGet("siguiente-numero-sugerido")]
    public async Task<IActionResult> GetSiguienteNumeroSugerido(CancellationToken cancellationToken)
    {
        var nextValue = await _empleadoNumberService.PeekNextAsync(cancellationToken);

        return Ok(new
        {
            numEmpleadoSugerido = nextValue
        });
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost("{id:int}/foto")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirFoto(int id, [FromForm] IFormFile file)
    {
        const long maxBytes = 2 * 1024 * 1024;
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };

        var empleado = await _db.Empleados.FindAsync(id);
        if (empleado is null)
            return NotFound(new { message = "Empleado no encontrado." });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Debes adjuntar una imagen." });

        if (file.Length > maxBytes)
            return BadRequest(new { message = "La foto no debe exceder 2 MB." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Formato no permitido. Usa JPG, PNG o WEBP." });

        if (!allowedMimeTypes.Contains(file.ContentType))
            return BadRequest(new { message = "Tipo MIME no permitido." });

        var webRoot = EnsureWebRootPath();
        var root = Path.Combine(webRoot, "storage", "empleados", id.ToString());
        Directory.CreateDirectory(root);

        if (!string.IsNullOrWhiteSpace(empleado.FotoRutaRelativa))
        {
            var oldPhysicalPath = Path.Combine(webRoot, empleado.FotoRutaRelativa.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(oldPhysicalPath))
                System.IO.File.Delete(oldPhysicalPath);
        }

        var fileName = $"perfil{extension}";
        var physicalPath = Path.Combine(root, fileName);

        await using (var stream = System.IO.File.Create(physicalPath))
        {
            await file.CopyToAsync(stream);
        }

        empleado.FotoNombreOriginal = file.FileName;
        empleado.FotoNombreGuardado = fileName;
        empleado.FotoRutaRelativa = Path.Combine("storage", "empleados", id.ToString(), fileName).Replace("\\", "/");
        empleado.FotoMimeType = file.ContentType;
        empleado.FotoTamanoBytes = file.Length;
        empleado.FotoUpdatedAtUtc = DateTime.UtcNow;
        empleado.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            empleado.Id,
            empleado.NumEmpleado,
            FotoUrl = BuildFotoApiUrl(empleado.Id, empleado.FotoUpdatedAtUtc),
            empleado.FotoNombreOriginal,
            empleado.FotoMimeType,
            empleado.FotoTamanoBytes,
            empleado.FotoUpdatedAtUtc
        });
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpDelete("{id:int}/foto")]
    public async Task<IActionResult> EliminarFoto(int id)
    {
        var empleado = await _db.Empleados.FindAsync(id);
        if (empleado is null)
            return NotFound(new { message = "Empleado no encontrado." });

        if (!string.IsNullOrWhiteSpace(empleado.FotoRutaRelativa))
        {
            var webRoot = EnsureWebRootPath();
            var physicalPath = Path.Combine(webRoot, empleado.FotoRutaRelativa.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physicalPath))
                System.IO.File.Delete(physicalPath);
        }

        empleado.FotoNombreOriginal = null;
        empleado.FotoNombreGuardado = null;
        empleado.FotoRutaRelativa = null;
        empleado.FotoMimeType = null;
        empleado.FotoTamanoBytes = null;
        empleado.FotoUpdatedAtUtc = DateTime.UtcNow;
        empleado.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost("{id:int}/baja")]
    public async Task<IActionResult> DarBaja(int id, [FromBody] DarBajaEmpleadoDto dto)
    {
        var empleado = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (empleado is null)
            return NotFound(new { message = "Empleado no existe." });

        if (!empleado.Activo || empleado.EstatusLaboralActual == EstatusLaboralEmpleado.BAJA)
            return Conflict(new { message = "El empleado ya está dado de baja." });

        if (dto.FechaBaja < empleado.FechaIngreso)
            return BadRequest(new { message = "La fecha de baja no puede ser menor a la fecha de ingreso." });

        await using var tx = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);

        empleado.Activo = false;
        empleado.EstatusLaboralActual = EstatusLaboralEmpleado.BAJA;
        empleado.FechaBajaActual = dto.FechaBaja;
        empleado.TipoBajaActual = dto.TipoBaja;
        empleado.Recontratable = dto.Recontratable ?? false;
        empleado.FechaReingresoActual = null;
        empleado.UpdatedAtUtc = DateTime.UtcNow;

        if (dto.DesactivarUsuario)
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.EmpleadoId == empleado.Id);
            if (user is not null)
            {
                user.IsActive = false;
                user.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await _empleadoMovimientoLaboralService.RegistrarBajaAsync(
            empleado,
            dto.FechaBaja,
            dto.TipoBaja,
            NormalizeNullable(dto.Motivo),
            NormalizeNullable(dto.Comentario),
            dto.Recontratable,
            TryGetCurrentUserId(),
            HttpContext.RequestAborted);

        // Regla de negocio:
        // Al dar de baja a un empleado, sus periodos vacacionales abiertos se cierran.
        // No se borra historial, no se descuenta saldo y se deja evidencia en kárdex
        // mediante movimiento CANCELACION con 0 días.
        var periodosVacacionesCerrados = await _vacacionesService.CerrarPeriodosAbiertosPorBajaAsync(
            empleado.Id,
            dto.FechaBaja,
            TryGetCurrentUserId(),
            HttpContext.RequestAborted);

        await tx.CommitAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            message = "Empleado dado de baja correctamente.",
            vacaciones = new
            {
                periodosCerrados = periodosVacacionesCerrados
            },
            empleado = new
            {
                empleado.Id,
                empleado.NumEmpleado,
                empleado.Nombres,
                empleado.ApellidoPaterno,
                empleado.ApellidoMaterno,
                empleado.Activo,
                empleado.EstatusLaboralActual,
                empleado.FechaBajaActual,
                empleado.TipoBajaActual,
                empleado.Recontratable
            }
        });
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost("{id:int}/reingreso")]
    public async Task<IActionResult> Reingresar(int id, [FromBody] ReingresarEmpleadoDto dto)
    {
        var empleado = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (empleado is null)
            return NotFound(new { message = "Empleado no existe." });

        if (empleado.Activo && empleado.EstatusLaboralActual == EstatusLaboralEmpleado.ACTIVO)
            return Conflict(new { message = "El empleado ya está activo." });

        if (dto.FechaReingreso < empleado.FechaIngreso)
            return BadRequest(new { message = "La fecha de reingreso no puede ser menor a la fecha de ingreso original." });

        if (empleado.FechaBajaActual.HasValue && dto.FechaReingreso < empleado.FechaBajaActual.Value)
            return BadRequest(new { message = "La fecha de reingreso no puede ser menor a la última fecha de baja." });

        var departamentoId = dto.DepartamentoId ?? empleado.DepartamentoId;
        var puestoId = dto.PuestoId ?? empleado.PuestoId;
        var sucursalId = dto.SucursalId ?? empleado.SucursalId;

        var relationResult = await ResolveRelationsAsync(departamentoId, puestoId, sucursalId);
        if (relationResult.Error is not null)
            return relationResult.Error;

        await using var tx = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);

        empleado.Activo = true;
        empleado.EstatusLaboralActual = EstatusLaboralEmpleado.ACTIVO;
        empleado.FechaReingresoActual = dto.FechaReingreso;
        empleado.FechaBajaActual = null;
        empleado.TipoBajaActual = null;
        empleado.Recontratable = true;
        empleado.DepartamentoId = relationResult.DepartamentoId;
        empleado.PuestoId = puestoId;
        empleado.SucursalId = relationResult.SucursalId;
        empleado.UpdatedAtUtc = DateTime.UtcNow;

        if (dto.ReactivarUsuario)
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.EmpleadoId == empleado.Id);
            if (user is not null)
            {
                user.IsActive = true;
                user.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await _empleadoMovimientoLaboralService.RegistrarReingresoAsync(
            empleado,
            dto.FechaReingreso,
            NormalizeNullable(dto.Comentario),
            TryGetCurrentUserId(),
            HttpContext.RequestAborted);

        await tx.CommitAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            message = "Empleado reingresado correctamente.",
            empleado = new
            {
                empleado.Id,
                empleado.NumEmpleado,
                empleado.Nombres,
                empleado.ApellidoPaterno,
                empleado.ApellidoMaterno,
                empleado.Activo,
                empleado.EstatusLaboralActual,
                empleado.FechaReingresoActual,
                empleado.DepartamentoId,
                empleado.PuestoId,
                empleado.SucursalId
            }
        });
    }

    [HttpGet("{id:int}/movimientos")]
    public async Task<IActionResult> GetMovimientos(int id)
    {
        var accessError = await EnsureCanAccessEmpleadoAsync(id);
        if (accessError is not null)
            return accessError;

        var empleadoExists = await _db.Empleados
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, HttpContext.RequestAborted);

        if (!empleadoExists)
            return NotFound(new { message = "Empleado no existe." });

        var items = await _db.EmpleadoMovimientosLaborales
            .AsNoTracking()
            .Where(x => x.EmpleadoId == id)
            .OrderByDescending(x => x.FechaMovimiento)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => ToMovimientoDto(x))
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(items);
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound(new { message = "Empleado no encontrado." });

        if (!entity.Activo)
            return NoContent();

        await using var tx = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);

        entity.Activo = false;
        entity.EstatusLaboralActual = EstatusLaboralEmpleado.BAJA;
        entity.FechaBajaActual ??= DateOnly.FromDateTime(DateTime.Today);
        entity.FechaReingresoActual = null;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _empleadoMovimientoLaboralService.RegistrarBajaAsync(
            entity,
            entity.FechaBajaActual.Value,
            entity.TipoBajaActual,
            "Baja administrativa",
            "Desactivación administrativa desde endpoint DELETE.",
            entity.Recontratable,
            TryGetCurrentUserId(),
            HttpContext.RequestAborted);

        // Regla espejo de DarBaja:
        // Si alguien usa DELETE para desactivar al empleado, también cerramos
        // los periodos abiertos de vacaciones para mantener consistencia operativa.
        await _vacacionesService.CerrarPeriodosAbiertosPorBajaAsync(
            entity.Id,
            entity.FechaBajaActual.Value,
            TryGetCurrentUserId(),
            HttpContext.RequestAborted);

        await tx.CommitAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    [Authorize(Roles = "ADMIN,RRHH")]
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var entity = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound(new { message = "Empleado no encontrado." });

        if (entity.Activo)
            return NoContent();

        await using var tx = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);

        entity.Activo = true;
        entity.EstatusLaboralActual = EstatusLaboralEmpleado.ACTIVO;
        entity.FechaReingresoActual = DateOnly.FromDateTime(DateTime.Today);
        entity.FechaBajaActual = null;
        entity.TipoBajaActual = null;
        entity.Recontratable = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _empleadoMovimientoLaboralService.RegistrarReingresoAsync(
            entity,
            entity.FechaReingresoActual.Value,
            "Reactivación administrativa desde endpoint restore.",
            TryGetCurrentUserId(),
            HttpContext.RequestAborted);

        await tx.CommitAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    private async Task<(int? DepartamentoId, int? SucursalId, IActionResult? Error)> ResolveRelationsAsync(
        int? departamentoId,
        int? puestoId,
        int? sucursalId)
    {
        Departamento? departamento = null;
        Puesto? puesto = null;

        if (departamentoId.HasValue)
        {
            departamento = await _db.Departamentos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == departamentoId.Value && x.Activo);

            if (departamento is null)
                return (null, null, BadRequest(new { message = "El departamento indicado no existe o está inactivo." }));
        }

        if (puestoId.HasValue)
        {
            puesto = await _db.Puestos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == puestoId.Value && x.Activo);

            if (puesto is null)
                return (null, null, BadRequest(new { message = "El puesto indicado no existe o está inactivo." }));

            if (departamentoId.HasValue && puesto.DepartamentoId != departamentoId.Value)
                return (null, null, BadRequest(new { message = "El puesto no pertenece al departamento indicado." }));

            departamentoId = puesto.DepartamentoId;
        }

        if (sucursalId.HasValue)
        {
            var sucursal = await _db.Sucursales
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == sucursalId.Value);

            if (sucursal is null)
                return (null, null, BadRequest(new { message = "La sucursal indicada no existe." }));

            if (!sucursal.Activo)
                return (null, null, BadRequest(new { message = "La sucursal indicada está inactiva." }));
        }

        return (departamentoId, sucursalId, null);
    }

    private async Task<(int? AprobadorPrimarioEmpleadoId, int? AprobadorSecundarioEmpleadoId, IActionResult? Error)> ResolveAprobadoresAsync(
        int? aprobadorPrimarioEmpleadoId,
        int? aprobadorSecundarioEmpleadoId,
        int? currentEmpleadoId)
    {
        if (currentEmpleadoId.HasValue)
        {
            if (aprobadorPrimarioEmpleadoId.HasValue && aprobadorPrimarioEmpleadoId.Value == currentEmpleadoId.Value)
            {
                return (null, null, BadRequest(new
                {
                    message = "El empleado no puede ser su propio aprobador primario."
                }));
            }

            if (aprobadorSecundarioEmpleadoId.HasValue && aprobadorSecundarioEmpleadoId.Value == currentEmpleadoId.Value)
            {
                return (null, null, BadRequest(new
                {
                    message = "El empleado no puede ser su propio aprobador secundario."
                }));
            }
        }

        if (aprobadorPrimarioEmpleadoId.HasValue &&
            aprobadorSecundarioEmpleadoId.HasValue &&
            aprobadorPrimarioEmpleadoId.Value == aprobadorSecundarioEmpleadoId.Value)
        {
            return (null, null, BadRequest(new
            {
                message = "El aprobador primario y el secundario no pueden ser el mismo empleado."
            }));
        }

        if (aprobadorPrimarioEmpleadoId.HasValue)
        {
            var primario = await _db.Empleados
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == aprobadorPrimarioEmpleadoId.Value);

            if (primario is null)
            {
                return (null, null, BadRequest(new
                {
                    message = "El aprobador primario indicado no existe."
                }));
            }

            if (!primario.Activo)
            {
                return (null, null, BadRequest(new
                {
                    message = "El aprobador primario indicado está inactivo."
                }));
            }
        }

        if (aprobadorSecundarioEmpleadoId.HasValue)
        {
            var secundario = await _db.Empleados
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == aprobadorSecundarioEmpleadoId.Value);

            if (secundario is null)
            {
                return (null, null, BadRequest(new
                {
                    message = "El aprobador secundario indicado no existe."
                }));
            }

            if (!secundario.Activo)
            {
                return (null, null, BadRequest(new
                {
                    message = "El aprobador secundario indicado está inactivo."
                }));
            }
        }

        return (aprobadorPrimarioEmpleadoId, aprobadorSecundarioEmpleadoId, null);
    }

    private async Task<IActionResult?> ValidateDuplicateIdentityAsync(
        int? currentEmpleadoId,
        string? curp,
        string? rfc,
        string? nss)
    {
        if (!string.IsNullOrWhiteSpace(curp))
        {
            var exists = await _db.Empleados.AsNoTracking()
                .AnyAsync(x => x.Curp == curp && (!currentEmpleadoId.HasValue || x.Id != currentEmpleadoId.Value));

            if (exists)
                return Conflict(new { message = "La CURP ya está registrada en otro empleado." });
        }

        if (!string.IsNullOrWhiteSpace(rfc))
        {
            var exists = await _db.Empleados.AsNoTracking()
                .AnyAsync(x => x.Rfc == rfc && (!currentEmpleadoId.HasValue || x.Id != currentEmpleadoId.Value));

            if (exists)
                return Conflict(new { message = "El RFC ya está registrado en otro empleado." });
        }

        if (!string.IsNullOrWhiteSpace(nss))
        {
            var exists = await _db.Empleados.AsNoTracking()
                .AnyAsync(x => x.Nss == nss && (!currentEmpleadoId.HasValue || x.Id != currentEmpleadoId.Value));

            if (exists)
                return Conflict(new { message = "El NSS ya está registrado en otro empleado." });
        }

        return null;
    }

    private IActionResult? ValidateEmpleadoExtendedFields(
        string? curp,
        string? rfc,
        string? nss,
        string? telefono,
        string? codigoPostal,
        string? codigoPostalFiscal,
        string? contactoEmergenciaTelefono)
    {
        var normalizedCurp = NormalizeUpperNullable(curp);
        if (!string.IsNullOrWhiteSpace(normalizedCurp) && !CurpRegex.IsMatch(normalizedCurp))
            return BadRequest(new { message = "La CURP no tiene un formato válido." });

        var normalizedRfc = NormalizeUpperNullable(rfc);
        if (!string.IsNullOrWhiteSpace(normalizedRfc) && !RfcRegex.IsMatch(normalizedRfc))
            return BadRequest(new { message = "El RFC no tiene un formato válido." });

        var normalizedNss = DigitsOnly(nss);
        if (!string.IsNullOrWhiteSpace(normalizedNss) && !NssRegex.IsMatch(normalizedNss))
            return BadRequest(new { message = "El NSS debe contener exactamente 11 dígitos." });

        var normalizedTelefono = DigitsOnly(telefono);
        if (!string.IsNullOrWhiteSpace(normalizedTelefono) && !PhoneRegex.IsMatch(normalizedTelefono))
            return BadRequest(new { message = "El teléfono debe contener entre 10 y 15 dígitos." });

        var normalizedCp = DigitsOnly(codigoPostal);
        if (!string.IsNullOrWhiteSpace(normalizedCp) && !CpRegex.IsMatch(normalizedCp))
            return BadRequest(new { message = "El código postal debe contener exactamente 5 dígitos." });

        var normalizedCpFiscal = DigitsOnly(codigoPostalFiscal);
        if (!string.IsNullOrWhiteSpace(normalizedCpFiscal) && !CpRegex.IsMatch(normalizedCpFiscal))
            return BadRequest(new { message = "El código postal fiscal debe contener exactamente 5 dígitos." });

        var normalizedContactoTelefono = DigitsOnly(contactoEmergenciaTelefono);
        if (!string.IsNullOrWhiteSpace(normalizedContactoTelefono) && !PhoneRegex.IsMatch(normalizedContactoTelefono))
            return BadRequest(new { message = "El teléfono de emergencia debe contener entre 10 y 15 dígitos." });

        return null;
    }

    private async Task<EmpleadoDto?> LoadEmpleadoDtoAsync(int id)
    {
        var empleado = await _db.Empleados
            .AsNoTracking()
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Include(x => x.Sucursal)
            .Include(x => x.AprobadorPrimario)
            .Include(x => x.AprobadorSecundario)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (empleado is null)
            return null;

        var dto = MapEmpleadoDto(empleado);

        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.EmpleadoId == id)
            .Select(x => new LinkedUserInfo
            {
                Id = x.Id,
                Email = x.Email,
                Role = x.Role
            })
            .FirstOrDefaultAsync();

        ApplyLinkedUserInfo(dto, user);

        return dto;
    }

    private EmpleadoDto MapEmpleadoDto(Empleado empleado)
    {
        return new EmpleadoDto
        {
            Id = empleado.Id,
            NumEmpleado = empleado.NumEmpleado,
            Nombres = empleado.Nombres,
            ApellidoPaterno = empleado.ApellidoPaterno,
            ApellidoMaterno = empleado.ApellidoMaterno,
            FechaNacimiento = empleado.FechaNacimiento,
            Telefono = empleado.Telefono,
            Email = empleado.Email,
            FechaIngreso = empleado.FechaIngreso,
            Activo = empleado.Activo,
            DepartamentoId = empleado.DepartamentoId,
            DepartamentoNombre = empleado.Departamento?.Nombre,
            PuestoId = empleado.PuestoId,
            PuestoNombre = empleado.Puesto?.Nombre,
            SucursalId = empleado.SucursalId,
            SucursalNombre = empleado.Sucursal?.Nombre,
            AprobadorPrimarioEmpleadoId = empleado.AprobadorPrimarioEmpleadoId,
            AprobadorPrimarioNombre = BuildNombreCompleto(empleado.AprobadorPrimario),
            AprobadorSecundarioEmpleadoId = empleado.AprobadorSecundarioEmpleadoId,
            AprobadorSecundarioNombre = BuildNombreCompleto(empleado.AprobadorSecundario),
            Curp = empleado.Curp,
            Rfc = empleado.Rfc,
            Nss = empleado.Nss,
            Sexo = empleado.Sexo,
            EstadoCivil = empleado.EstadoCivil,
            Nacionalidad = empleado.Nacionalidad,
            DireccionCalle = empleado.DireccionCalle,
            DireccionNumeroExterior = empleado.DireccionNumeroExterior,
            DireccionNumeroInterior = empleado.DireccionNumeroInterior,
            DireccionColonia = empleado.DireccionColonia,
            DireccionCiudad = empleado.DireccionCiudad,
            DireccionEstado = empleado.DireccionEstado,
            DireccionCodigoPostal = empleado.DireccionCodigoPostal,
            CodigoPostalFiscal = empleado.CodigoPostalFiscal,
            EntidadFiscal = empleado.EntidadFiscal,
            ContactoEmergenciaNombre = empleado.ContactoEmergenciaNombre,
            ContactoEmergenciaTelefono = empleado.ContactoEmergenciaTelefono,
            ContactoEmergenciaParentesco = empleado.ContactoEmergenciaParentesco,
            EstatusLaboralActual = empleado.EstatusLaboralActual,
            FechaBajaActual = empleado.FechaBajaActual,
            TipoBajaActual = empleado.TipoBajaActual,
            FechaReingresoActual = empleado.FechaReingresoActual,
            Recontratable = empleado.Recontratable,
            FotoUrl = !string.IsNullOrWhiteSpace(empleado.FotoRutaRelativa)
                ? BuildFotoApiUrl(empleado.Id, empleado.FotoUpdatedAtUtc)
                : null,
            TieneFoto = !string.IsNullOrWhiteSpace(empleado.FotoRutaRelativa),
            FotoNombreOriginal = empleado.FotoNombreOriginal,
            FotoMimeType = empleado.FotoMimeType,
            FotoTamanoBytes = empleado.FotoTamanoBytes,
            CreatedAtUtc = empleado.CreatedAtUtc,
            UpdatedAtUtc = empleado.UpdatedAtUtc
        };
    }

    private static EmpleadoMovimientoLaboralDto ToMovimientoDto(EmpleadoMovimientoLaboral x)
    {
        return new EmpleadoMovimientoLaboralDto
        {
            Id = x.Id,
            EmpleadoId = x.EmpleadoId,
            TipoMovimiento = x.TipoMovimiento,
            FechaMovimiento = x.FechaMovimiento,
            TipoBaja = x.TipoBaja,
            Motivo = x.Motivo,
            Comentario = x.Comentario,
            Recontratable = x.Recontratable,
            UsuarioResponsableId = x.UsuarioResponsableId,
            CreatedAtUtc = x.CreatedAtUtc
        };
    }

    private async Task<Dictionary<int, LinkedUserInfo>> LoadUsersByEmpleadoIdsAsync(List<int> empleadoIds)
    {
        if (empleadoIds.Count == 0)
            return new Dictionary<int, LinkedUserInfo>();

        return await _db.Users
            .AsNoTracking()
            .Where(x => x.EmpleadoId.HasValue && empleadoIds.Contains(x.EmpleadoId.Value))
            .Select(x => new
            {
                EmpleadoId = x.EmpleadoId!.Value,
                User = new LinkedUserInfo
                {
                    Id = x.Id,
                    Email = x.Email,
                    Role = x.Role
                }
            })
            .ToDictionaryAsync(x => x.EmpleadoId, x => x.User);
    }

    private static void ApplyLinkedUserInfo(EmpleadoDto dto, LinkedUserInfo? user)
    {
        dto.TieneCuenta = user is not null;
        dto.UsuarioId = user?.Id;
        dto.UsuarioEmail = user?.Email;
        dto.UsuarioRole = user?.Role;
    }

    private async Task<IActionResult?> EnsureCanAccessEmpleadoAsync(int empleadoId)
    {
        var canView = await _empleadoAccessScopeService.CanViewEmpleadoAsync(
            User,
            empleadoId,
            HttpContext.RequestAborted);

        return canView ? null : Forbid();
    }

    private bool CurrentUserHasAnyRole(params string[] roles)
    {
        var allowed = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
            return false;

        var currentRoles = User.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role ||
                claim.Type == "role" ||
                claim.Type == "roles" ||
                claim.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(role => role.Trim().ToUpperInvariant())
            .Where(role => !string.IsNullOrWhiteSpace(role));

        return currentRoles.Any(allowed.Contains);
    }

    private async Task<int?> ResolveCurrentEmpleadoIdAsync(CancellationToken cancellationToken)
    {
        var empleadoIdFromClaim = TryGetIntClaimValue(
            "empleadoId",
            "EmpleadoId",
            "empleado_id",
            "rh_empleado_id");

        if (empleadoIdFromClaim.HasValue && empleadoIdFromClaim.Value > 0)
            return empleadoIdFromClaim.Value;

        var userId = TryGetCurrentUserId();
        if (userId.HasValue)
        {
            var empleadoIdByUserId = await _db.Users
                .AsNoTracking()
                .Where(x => x.Id == userId.Value && x.IsActive)
                .Select(x => x.EmpleadoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdByUserId.HasValue && empleadoIdByUserId.Value > 0)
                return empleadoIdByUserId.Value;
        }

        var email = GetCurrentUserEmail();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var empleadoIdByEmail = await _db.Users
                .AsNoTracking()
                .Where(x => x.IsActive && x.Email.ToLower() == normalizedEmail)
                .Select(x => x.EmpleadoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdByEmail.HasValue && empleadoIdByEmail.Value > 0)
                return empleadoIdByEmail.Value;

            var empleadoIdDirectByEmail = await _db.Empleados
                .AsNoTracking()
                .Where(x => x.Email != null && x.Email.ToLower() == normalizedEmail)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (empleadoIdDirectByEmail.HasValue && empleadoIdDirectByEmail.Value > 0)
                return empleadoIdDirectByEmail.Value;
        }

        return null;
    }

    private int? TryGetCurrentUserId()
    {
        return TryGetIntClaimValue(
            ClaimTypes.NameIdentifier,
            "sub",
            ClaimTypes.Sid,
            "nameid",
            "userId",
            "UserId",
            "uid");
    }

    private int? TryGetIntClaimValue(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = User.FindFirstValue(claimType);
            if (int.TryParse(value, out var parsed) && parsed > 0)
                return parsed;
        }

        return null;
    }

    private string? GetCurrentUserEmail()
    {
        var value =
            User.FindFirstValue(ClaimTypes.Email) ??
            User.FindFirstValue("email") ??
            User.FindFirstValue("preferred_username") ??
            User.FindFirstValue("unique_name") ??
            User.FindFirstValue("upn") ??
            User.FindFirstValue(ClaimTypes.Name) ??
            User.Identity?.Name;

        var normalized = NormalizeNullable(value);
        return normalized?.Contains('@') == true ? normalized : null;
    }

    private BadRequestObjectResult? ValidateImportFile(IFormFile? file)
    {
        if (file is null)
            return BadRequest(new { message = "Debes adjuntar un archivo Excel." });

        if (file.Length <= 0)
            return BadRequest(new { message = "El archivo Excel está vacío." });

        if (file.Length > MaxImportFileBytes)
            return BadRequest(new { message = "El archivo Excel no debe exceder 10 MB." });

        var extension = Path.GetExtension(file.FileName)?.Trim().ToLowerInvariant();
        if (extension != ".xlsx")
            return BadRequest(new { message = "Formato no permitido. Debes subir un archivo .xlsx." });

        return null;
    }

    private static string? NormalizeNullable(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeLowerNullable(string? value)
    {
        var normalized = NormalizeNullable(value);
        return normalized?.ToLowerInvariant();
    }

    private static string? NormalizeUpperNullable(string? value)
    {
        var normalized = NormalizeNullable(value);
        return normalized?.ToUpperInvariant();
    }

    private static string? DigitsOnly(string? value)
    {
        var normalized = NormalizeNullable(value);
        if (normalized is null)
            return null;

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string? BuildNombreCompleto(Empleado? empleado)
    {
        if (empleado is null)
            return null;

        var parts = new[]
        {
            empleado.Nombres?.Trim(),
            empleado.ApellidoPaterno?.Trim(),
            empleado.ApellidoMaterno?.Trim()
        }
        .Where(x => !string.IsNullOrWhiteSpace(x));

        var nombre = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(nombre) ? null : nombre;
    }

    private static string BuildFotoApiUrl(int empleadoId, DateTime? updatedAtUtc)
    {
        var version = (updatedAtUtc ?? DateTime.UtcNow).Ticks;
        return $"/api/Empleados/{empleadoId}/foto?v={version}";
    }

    private string EnsureWebRootPath()
    {
        var webRoot = _env.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            Directory.CreateDirectory(webRoot);
        }

        return webRoot;
    }

    private sealed class LinkedUserInfo
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}