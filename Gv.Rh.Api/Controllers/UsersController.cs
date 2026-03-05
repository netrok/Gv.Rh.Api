using Gv.Rh.Api.Models;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class UsersController : ControllerBase
{
    private readonly RhDbContext _db;

    public UsersController(RhDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? active = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => EF.Functions.ILike(x.Email, $"%{q.Trim()}%"));

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(x => x.Role == role.Trim());

        if (active.HasValue)
            query = query.Where(x => x.IsActive == active.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.Role,
                x.IsActive,
                x.MustChangePassword,
                x.EmpleadoId,
                x.CreatedAtUtc
            })
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
        var user = await _db.Users.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.Role,
                x.IsActive,
                x.MustChangePassword,
                x.EmpleadoId,
                x.CreatedAtUtc
            })
            .FirstOrDefaultAsync();

        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserCreateDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(x => x.Email.ToLower() == email);
        if (exists) return Conflict(new { message = "Email ya existe." });

        if (dto.EmpleadoId.HasValue)
        {
            var empleadoExists = await _db.Empleados.AnyAsync(e => e.Id == dto.EmpleadoId.Value);
            if (!empleadoExists) return BadRequest(new { message = "EmpleadoId no existe." });

            var empleadoAlreadyLinked = await _db.Users.AnyAsync(u => u.EmpleadoId == dto.EmpleadoId.Value);
            if (empleadoAlreadyLinked) return Conflict(new { message = "Ese empleado ya tiene cuenta." });
        }

        var user = new AppUser
        {
            Email = email,
            Role = dto.Role.Trim(),
            IsActive = dto.IsActive,
            EmpleadoId = dto.EmpleadoId,
            MustChangePassword = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, new
        {
            user.Id,
            user.Email,
            user.Role,
            user.IsActive,
            user.MustChangePassword,
            user.EmpleadoId,
            user.CreatedAtUtc
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null) return NotFound();

        if (dto.EmpleadoId.HasValue)
        {
            var empleadoExists = await _db.Empleados.AnyAsync(e => e.Id == dto.EmpleadoId.Value);
            if (!empleadoExists) return BadRequest(new { message = "EmpleadoId no existe." });

            var empleadoAlreadyLinked = await _db.Users.AnyAsync(u => u.Id != id && u.EmpleadoId == dto.EmpleadoId.Value);
            if (empleadoAlreadyLinked) return Conflict(new { message = "Ese empleado ya tiene cuenta." });
        }

        user.Role = dto.Role.Trim();
        user.IsActive = dto.IsActive;
        user.EmpleadoId = dto.EmpleadoId;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.Role,
            user.IsActive,
            user.MustChangePassword,
            user.EmpleadoId
        });
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null) return NotFound();

        var newPass = string.IsNullOrWhiteSpace(dto.NewPassword)
            ? GenerateTempPassword()
            : dto.NewPassword!.Trim();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPass);
        user.MustChangePassword = true;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Password actualizado.", tempPassword = newPass });
    }

    // ✅ Vincular empleado existente al usuario por email (solo ADMIN)
    // POST /api/users/{id}/link-empleado-by-email
    [HttpPost("{id:int}/link-empleado-by-email")]
    public async Task<IActionResult> LinkEmpleadoByEmail(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(new { message = "Usuario no existe." });

        var email = user.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "El usuario no tiene Email válido." });

        if (user.EmpleadoId.HasValue)
            return Conflict(new { message = "Este usuario ya está ligado a un empleado." });

        var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.Email != null && e.Email.ToLower() == email);
        if (empleado is null)
            return NotFound(new { message = "No existe empleado con el email del usuario." });

        var empleadoAlreadyLinked = await _db.Users.AnyAsync(u => u.EmpleadoId == empleado.Id);
        if (empleadoAlreadyLinked)
            return Conflict(new { message = "Ese empleado ya tiene una cuenta ligada." });

        user.EmpleadoId = empleado.Id;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Empleado ligado al usuario por email.",
            user = new { user.Id, user.Email, user.Role, user.IsActive, user.MustChangePassword, user.EmpleadoId },
            empleado = new { empleado.Id, empleado.NumEmpleado, empleado.Nombres, empleado.ApellidoPaterno, empleado.ApellidoMaterno, empleado.Email }
        });
    }

    private static string GenerateTempPassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(9);
        var base64 = Convert.ToBase64String(bytes)
            .Replace("=", "")
            .Replace("+", "A")
            .Replace("/", "B");

        return $"Tmp-{base64}*1";
    }
}