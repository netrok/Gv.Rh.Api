using ClosedXML.Excel;
using Gv.Rh.Api.Models;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public class EmpleadosController : ControllerBase
{
    private readonly RhDbContext _db;

    public EmpleadosController(RhDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] bool? activo = true,
        [FromQuery] string sort = "id",
        [FromQuery] string dir = "desc")
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<Empleado> query = _db.Empleados.AsNoTracking();

        if (activo.HasValue)
            query = query.Where(e => e.Activo == activo.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(e =>
                EF.Functions.ILike(e.NumEmpleado, $"%{q}%") ||
                EF.Functions.ILike(e.Nombres, $"%{q}%") ||
                EF.Functions.ILike(e.ApellidoPaterno, $"%{q}%") ||
                (e.ApellidoMaterno != null && EF.Functions.ILike(e.ApellidoMaterno, $"%{q}%")) ||
                (e.Email != null && EF.Functions.ILike(e.Email, $"%{q}%"))
            );
        }

        bool asc = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);

        query = (sort?.ToLowerInvariant()) switch
        {
            "numempleado" => asc ? query.OrderBy(x => x.NumEmpleado) : query.OrderByDescending(x => x.NumEmpleado),
            "nombre" or "nombres" => asc ? query.OrderBy(x => x.Nombres) : query.OrderByDescending(x => x.Nombres),
            "apellido" or "apellidopaterno" => asc ? query.OrderBy(x => x.ApellidoPaterno) : query.OrderByDescending(x => x.ApellidoPaterno),
            "fechaingreso" => asc ? query.OrderBy(x => x.FechaIngreso) : query.OrderByDescending(x => x.FechaIngreso),
            "activo" => asc ? query.OrderBy(x => x.Activo) : query.OrderByDescending(x => x.Activo),
            _ => asc ? query.OrderBy(x => x.Id) : query.OrderByDescending(x => x.Id),
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
        var emp = await _db.Empleados.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return emp is null ? NotFound() : Ok(emp);
    }

    // GET /api/empleados/export.xlsx?q=...&activo=true
    [HttpGet("export.xlsx")]
    public async Task<IActionResult> ExportXlsx([FromQuery] string? q = null, [FromQuery] bool? activo = null)
    {
        var query = _db.Empleados.AsNoTracking().AsQueryable();

        if (activo.HasValue)
            query = query.Where(x => x.Activo == activo.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.NumEmpleado, $"%{term}%") ||
                EF.Functions.ILike(x.Nombres, $"%{term}%") ||
                EF.Functions.ILike(x.ApellidoPaterno, $"%{term}%") ||
                (x.ApellidoMaterno != null && EF.Functions.ILike(x.ApellidoMaterno, $"%{term}%")) ||
                (x.Email != null && EF.Functions.ILike(x.Email, $"%{term}%")) ||
                (x.Telefono != null && EF.Functions.ILike(x.Telefono, $"%{term}%"))
            );
        }

        var rows = await query
            .OrderBy(x => x.NumEmpleado)
            .Take(50000) // límite sano
            .Select(x => new
            {
                x.Id,
                x.NumEmpleado,
                x.Nombres,
                x.ApellidoPaterno,
                x.ApellidoMaterno,
                x.Email,
                x.Telefono,
                x.FechaIngreso, // DateOnly / DateOnly?
                x.Activo
            })
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Empleados");

        var headers = new[]
        {
            "Id","NumEmpleado","Nombres","ApellidoPaterno","ApellidoMaterno",
            "Email","Telefono","FechaIngreso","Activo"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.Id;
            ws.Cell(r, 2).Value = x.NumEmpleado;
            ws.Cell(r, 3).Value = x.Nombres;
            ws.Cell(r, 4).Value = x.ApellidoPaterno;
            ws.Cell(r, 5).Value = x.ApellidoMaterno ?? "";
            ws.Cell(r, 6).Value = x.Email ?? "";
            ws.Cell(r, 7).Value = x.Telefono ?? "";

            // ✅ DateOnly -> DateTime (Excel lo entiende perfecto)
            if (x.FechaIngreso is DateOnly d)
                ws.Cell(r, 8).Value = d.ToDateTime(TimeOnly.MinValue);
            else
                ws.Cell(r, 8).Value = ""; // por si algún día lo haces nullable

            ws.Cell(r, 9).Value = x.Activo;
            r++;
        }

        ws.Column(8).Style.DateFormat.Format = "yyyy-mm-dd";
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var bytes = ms.ToArray();
        var fileName = $"empleados_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // ✅ Crear cuenta para empleado (solo ADMIN)
    [Authorize(Roles = "ADMIN")]
    [HttpPost("{id:int}/create-account")]
    public async Task<IActionResult> CreateAccount(int id, [FromBody] CreateAccountForEmpleadoDto dto)
    {
        var empleadoExists = await _db.Empleados.AsNoTracking().AnyAsync(x => x.Id == id);
        if (!empleadoExists) return NotFound(new { message = "Empleado no existe." });

        var alreadyLinked = await _db.Users.AnyAsync(u => u.EmpleadoId == id);
        if (alreadyLinked) return Conflict(new { message = "Este empleado ya tiene cuenta." });

        var emailRaw = dto.Email?.Trim();
        if (string.IsNullOrWhiteSpace(emailRaw))
            return BadRequest(new { message = "Email es requerido." });

        var email = emailRaw.ToLowerInvariant();
        var emailExists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (emailExists) return Conflict(new { message = "Email ya existe." });

        var role = (dto.Role ?? "").Trim();
        if (string.IsNullOrWhiteSpace(role))
            return BadRequest(new { message = "Role es requerido." });

        var user = new AppUser
        {
            Email = email,
            Role = role,
            IsActive = dto.IsActive,
            EmpleadoId = id,
            MustChangePassword = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Cuenta creada y ligada al empleado.",
            user = new { user.Id, user.Email, user.Role, user.IsActive, user.MustChangePassword, user.EmpleadoId }
        });
    }

    // ✅ Vincular usuario existente al empleado por email (solo ADMIN)
    // POST /api/empleados/{id}/link-user-by-email
    [Authorize(Roles = "ADMIN")]
    [HttpPost("{id:int}/link-user-by-email")]
    public async Task<IActionResult> LinkUserByEmail(int id)
    {
        var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.Id == id);
        if (empleado is null) return NotFound(new { message = "Empleado no existe." });

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
            user = new { user.Id, user.Email, user.Role, user.IsActive, user.MustChangePassword, user.EmpleadoId },
            empleado = new { empleado.Id, empleado.NumEmpleado, empleado.Nombres, empleado.ApellidoPaterno, empleado.ApellidoMaterno, empleado.Email }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EmpleadoCreateDto dto)
    {
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
            Activo = dto.Activo
        };

        _db.Empleados.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] EmpleadoUpdateDto dto)
    {
        var entity = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        entity.Nombres = dto.Nombres.Trim();
        entity.ApellidoPaterno = dto.ApellidoPaterno.Trim();
        entity.ApellidoMaterno = string.IsNullOrWhiteSpace(dto.ApellidoMaterno) ? null : dto.ApellidoMaterno.Trim();
        entity.FechaNacimiento = dto.FechaNacimiento;
        entity.Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();
        entity.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        entity.FechaIngreso = dto.FechaIngreso;
        entity.Activo = dto.Activo;

        await _db.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        if (!entity.Activo) return NoContent();

        entity.Activo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var entity = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null) return NotFound();

        if (entity.Activo) return NoContent();

        entity.Activo = true;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<long> NextEmpleadoSequenceAsync()
    {
        var conn = _db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;

        if (shouldClose)
            await conn.OpenAsync();

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT nextval('empleados_num_seq')";
            var result = await cmd.ExecuteScalarAsync();

            if (result is null || result == DBNull.Value)
                throw new InvalidOperationException("No se pudo obtener el siguiente valor de la secuencia empleados_num_seq.");

            return Convert.ToInt64(result);
        }
        finally
        {
            if (shouldClose)
                await conn.CloseAsync();
        }
    }
}