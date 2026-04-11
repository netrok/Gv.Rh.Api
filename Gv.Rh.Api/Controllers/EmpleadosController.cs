using System.Data;
using System.Data.Common;
using System.Security.Claims;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Empleados;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public class EmpleadosController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly IEmpleadosReportService _empleadosReportService;

    public EmpleadosController(
        RhDbContext db,
        IEmpleadosReportService empleadosReportService)
    {
        _db = db;
        _empleadosReportService = empleadosReportService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] bool? activo = true,
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
            .Include(x => x.Sucursal);

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
                (e.Departamento != null && EF.Functions.ILike(e.Departamento.Nombre, $"%{q}%")) ||
                (e.Puesto != null && EF.Functions.ILike(e.Puesto.Nombre, $"%{q}%")) ||
                (e.Sucursal != null && EF.Functions.ILike(e.Sucursal.Nombre, $"%{q}%")) ||
                (e.Sucursal != null && EF.Functions.ILike(e.Sucursal.Clave, $"%{q}%"))
            );
        }

        var asc = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);

        query = (sort?.ToLowerInvariant()) switch
        {
            "numempleado" => asc ? query.OrderBy(x => x.NumEmpleado) : query.OrderByDescending(x => x.NumEmpleado),
            "nombre" or "nombres" => asc ? query.OrderBy(x => x.Nombres) : query.OrderByDescending(x => x.Nombres),
            "apellido" or "apellidopaterno" => asc ? query.OrderBy(x => x.ApellidoPaterno) : query.OrderByDescending(x => x.ApellidoPaterno),
            "fechaingreso" => asc ? query.OrderBy(x => x.FechaIngreso) : query.OrderByDescending(x => x.FechaIngreso),
            "departamento" => asc ? query.OrderBy(x => x.Departamento!.Nombre) : query.OrderByDescending(x => x.Departamento!.Nombre),
            "puesto" => asc ? query.OrderBy(x => x.Puesto!.Nombre) : query.OrderByDescending(x => x.Puesto!.Nombre),
            "sucursal" => asc ? query.OrderBy(x => x.Sucursal!.Nombre) : query.OrderByDescending(x => x.Sucursal!.Nombre),
            "activo" => asc ? query.OrderBy(x => x.Activo) : query.OrderByDescending(x => x.Activo),
            _ => asc ? query.OrderBy(x => x.Id) : query.OrderByDescending(x => x.Id),
        };

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToListAsync();

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
        var emp = await _db.Empleados
            .AsNoTracking()
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Include(x => x.Sucursal)
            .Where(x => x.Id == id)
            .Select(x => ToDto(x))
            .FirstOrDefaultAsync();

        return emp is null ? NotFound() : Ok(emp);
    }

    [HttpGet("export/xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] EmpleadosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _empleadosReportService.BuildXlsxAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] EmpleadosReporteQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _empleadosReportService.BuildPdfAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("{id:int}/create-account")]
    public async Task<IActionResult> CreateAccount(int id, [FromBody] CreateAccountForEmpleadoDto dto)
    {
        var empleadoExists = await _db.Empleados.AsNoTracking().AnyAsync(x => x.Id == id);
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

        var role = dto.Role.Trim();
        if (string.IsNullOrWhiteSpace(role))
            return BadRequest(new { message = "Role es requerido." });

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

    [Authorize(Roles = "ADMIN")]
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EmpleadoCreateDto dto)
    {
        var relationResult = await ResolveRelationsAsync(dto.DepartamentoId, dto.PuestoId, dto.SucursalId);
        if (relationResult.Error is not null)
            return relationResult.Error;

        var next = await NextEmpleadoSequenceAsync();
        var numEmpleado = $"EMP-{next:000000}";

        var entity = new Empleado
        {
            NumEmpleado = numEmpleado,
            Nombres = dto.Nombres.Trim(),
            ApellidoPaterno = dto.ApellidoPaterno.Trim(),
            ApellidoMaterno = string.IsNullOrWhiteSpace(dto.ApellidoMaterno) ? null : dto.ApellidoMaterno.Trim(),
            FechaNacimiento = dto.FechaNacimiento,
            Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            FechaIngreso = dto.FechaIngreso,
            Activo = dto.Activo,
            EstatusLaboralActual = dto.Activo ? EstatusLaboralEmpleado.ACTIVO : EstatusLaboralEmpleado.BAJA,
            DepartamentoId = relationResult.DepartamentoId,
            PuestoId = dto.PuestoId,
            SucursalId = relationResult.SucursalId
        };

        _db.Empleados.Add(entity);
        await _db.SaveChangesAsync();

        var created = await _db.Empleados
            .AsNoTracking()
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Include(x => x.Sucursal)
            .Where(x => x.Id == entity.Id)
            .Select(x => ToDto(x))
            .FirstAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] EmpleadoUpdateDto dto)
    {
        var entity = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound();

        var relationResult = await ResolveRelationsAsync(dto.DepartamentoId, dto.PuestoId, dto.SucursalId);
        if (relationResult.Error is not null)
            return relationResult.Error;

        entity.Nombres = dto.Nombres.Trim();
        entity.ApellidoPaterno = dto.ApellidoPaterno.Trim();
        entity.ApellidoMaterno = string.IsNullOrWhiteSpace(dto.ApellidoMaterno) ? null : dto.ApellidoMaterno.Trim();
        entity.FechaNacimiento = dto.FechaNacimiento;
        entity.Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();
        entity.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        entity.FechaIngreso = dto.FechaIngreso;
        entity.Activo = dto.Activo;
        entity.EstatusLaboralActual = dto.Activo ? EstatusLaboralEmpleado.ACTIVO : EstatusLaboralEmpleado.BAJA;
        entity.DepartamentoId = relationResult.DepartamentoId;
        entity.PuestoId = dto.PuestoId;
        entity.SucursalId = relationResult.SucursalId;

        await _db.SaveChangesAsync();

        var updated = await _db.Empleados
            .AsNoTracking()
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Include(x => x.Sucursal)
            .Where(x => x.Id == entity.Id)
            .Select(x => ToDto(x))
            .FirstAsync();

        return Ok(updated);
    }

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

        empleado.Activo = false;
        empleado.EstatusLaboralActual = EstatusLaboralEmpleado.BAJA;
        empleado.FechaBajaActual = dto.FechaBaja;
        empleado.TipoBajaActual = dto.TipoBaja;
        empleado.Recontratable = dto.Recontratable;
        empleado.FechaReingresoActual = null;

        var movimiento = new EmpleadoMovimientoLaboral
        {
            EmpleadoId = empleado.Id,
            TipoMovimiento = TipoMovimientoLaboral.BAJA,
            FechaMovimiento = dto.FechaBaja,
            TipoBaja = dto.TipoBaja,
            Motivo = string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim(),
            Comentario = string.IsNullOrWhiteSpace(dto.Comentario) ? null : dto.Comentario.Trim(),
            Recontratable = dto.Recontratable,
            UsuarioResponsableId = TryGetCurrentUserId(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.EmpleadoMovimientosLaborales.Add(movimiento);

        if (dto.DesactivarUsuario)
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.EmpleadoId == empleado.Id);
            if (user is not null)
            {
                user.IsActive = false;
                user.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Empleado dado de baja correctamente.",
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

        empleado.Activo = true;
        empleado.EstatusLaboralActual = EstatusLaboralEmpleado.ACTIVO;
        empleado.FechaReingresoActual = dto.FechaReingreso;
        empleado.FechaBajaActual = null;
        empleado.TipoBajaActual = null;
        empleado.Recontratable = null;
        empleado.DepartamentoId = relationResult.DepartamentoId;
        empleado.PuestoId = puestoId;
        empleado.SucursalId = relationResult.SucursalId;

        var movimiento = new EmpleadoMovimientoLaboral
        {
            EmpleadoId = empleado.Id,
            TipoMovimiento = TipoMovimientoLaboral.REINGRESO,
            FechaMovimiento = dto.FechaReingreso,
            Motivo = "Reingreso",
            Comentario = string.IsNullOrWhiteSpace(dto.Comentario) ? null : dto.Comentario.Trim(),
            Recontratable = null,
            UsuarioResponsableId = TryGetCurrentUserId(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.EmpleadoMovimientosLaborales.Add(movimiento);

        if (dto.ReactivarUsuario)
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.EmpleadoId == empleado.Id);
            if (user is not null)
            {
                user.IsActive = true;
                user.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

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
        var empleadoExists = await _db.Empleados
            .AsNoTracking()
            .AnyAsync(x => x.Id == id);

        if (!empleadoExists)
            return NotFound(new { message = "Empleado no existe." });

        var items = await _db.EmpleadoMovimientosLaborales
            .AsNoTracking()
            .Where(x => x.EmpleadoId == id)
            .OrderByDescending(x => x.FechaMovimiento)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => ToMovimientoDto(x))
            .ToListAsync();

        return Ok(items);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound();

        if (!entity.Activo)
            return NoContent();

        entity.Activo = false;
        entity.EstatusLaboralActual = EstatusLaboralEmpleado.BAJA;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var entity = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return NotFound();

        if (entity.Activo)
            return NoContent();

        entity.Activo = true;
        entity.EstatusLaboralActual = EstatusLaboralEmpleado.ACTIVO;
        await _db.SaveChangesAsync();

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

    private static EmpleadoDto ToDto(Empleado x)
    {
        return new EmpleadoDto
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
            EstatusLaboralActual = x.EstatusLaboralActual,
            FechaBajaActual = x.FechaBajaActual,
            TipoBajaActual = x.TipoBajaActual,
            FechaReingresoActual = x.FechaReingresoActual,
            Recontratable = x.Recontratable,
            DepartamentoId = x.DepartamentoId,
            DepartamentoNombre = x.Departamento != null ? x.Departamento.Nombre : null,
            PuestoId = x.PuestoId,
            PuestoNombre = x.Puesto != null ? x.Puesto.Nombre : null,
            SucursalId = x.SucursalId,
            SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null
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

    private int? TryGetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue(ClaimTypes.Sid) ??
            User.FindFirstValue("nameid");

        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static async Task EnsureEmpleadoSequenceAsync(DbConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DO $$
DECLARE
    v_max_num bigint;
    v_last_value bigint;
    v_is_called boolean;
    v_next_value bigint;
BEGIN
    CREATE SEQUENCE IF NOT EXISTS public.empleados_num_seq
        AS bigint
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        NO CYCLE;

    SELECT COALESCE(
        MAX(NULLIF(regexp_replace(""NumEmpleado"", '\D', '', 'g'), '')::bigint),
        0
    )
    INTO v_max_num
    FROM empleados;

    SELECT last_value, is_called
    INTO v_last_value, v_is_called
    FROM public.empleados_num_seq;

    v_next_value := GREATEST(
        v_max_num + 1,
        CASE
            WHEN v_is_called THEN v_last_value + 1
            ELSE v_last_value
        END
    );

    PERFORM setval('public.empleados_num_seq', v_next_value, false);
END $$;
";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> NextEmpleadoSequenceAsync()
    {
        var conn = _db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;

        if (shouldClose)
            await conn.OpenAsync();

        try
        {
            await EnsureEmpleadoSequenceAsync(conn);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT nextval('public.empleados_num_seq')";
            var result = await cmd.ExecuteScalarAsync();

            if (result is null || result == DBNull.Value)
                throw new InvalidOperationException("No se pudo obtener el siguiente valor de la secuencia public.empleados_num_seq.");

            return Convert.ToInt64(result);
        }
        finally
        {
            if (shouldClose)
                await conn.CloseAsync();
        }
    }
}