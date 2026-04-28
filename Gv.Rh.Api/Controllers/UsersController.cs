using Gv.Rh.Api.Models;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = UserRoles.Admin)]
public class UsersController : ControllerBase
{
    private readonly RhDbContext _db;

    public UsersController(RhDbContext db)
    {
        _db = db;
    }

    [HttpGet("roles")]
    public IActionResult GetRoles()
    {
        var roles = UserRoles.All
            .Select(x => new
            {
                value = x,
                label = x
            })
            .ToList();

        return Ok(roles);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? active = null,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Users
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Email, $"%{term}%") ||
                EF.Functions.ILike(x.FullName, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole = UserRoles.Normalize(role);

            if (!UserRoles.IsValid(normalizedRole))
            {
                return BadRequest(new
                {
                    message = $"Rol inválido. Permitidos: {string.Join(", ", UserRoles.All)}"
                });
            }

            query = query.Where(x => x.Role == normalizedRole);
        }

        if (active.HasValue)
        {
            query = query.Where(x => x.IsActive == active.Value);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FullName,
                role = UserRoles.Normalize(x.Role),
                x.IsActive,
                x.MustChangePassword,
                x.EmpleadoId,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync(ct);

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
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FullName,
                role = UserRoles.Normalize(x.Role),
                x.IsActive,
                x.MustChangePassword,
                x.EmpleadoId,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserCreateDto dto, CancellationToken ct = default)
    {
        if (dto is null)
        {
            return BadRequest(new { message = "Payload inválido." });
        }

        var email = NormalizeEmail(dto.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email es obligatorio." });
        }

        var password = (dto.Password ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "Password es obligatorio." });
        }

        var normalizedRole = UserRoles.Normalize(dto.Role);
        if (!UserRoles.IsValid(normalizedRole))
        {
            return BadRequest(new
            {
                message = $"Rol inválido. Permitidos: {string.Join(", ", UserRoles.All)}"
            });
        }

        var exists = await _db.Users.AnyAsync(x => x.Email == email, ct);
        if (exists)
        {
            return Conflict(new { message = "Email ya existe." });
        }

        Empleado? empleado = null;

        if (dto.EmpleadoId.HasValue)
        {
            empleado = await _db.Empleados.FirstOrDefaultAsync(
                e => e.Id == dto.EmpleadoId.Value, ct);

            if (empleado is null)
            {
                return BadRequest(new { message = "EmpleadoId no existe." });
            }

            var empleadoAlreadyLinked = await _db.Users.AnyAsync(
                u => u.EmpleadoId == dto.EmpleadoId.Value, ct);

            if (empleadoAlreadyLinked)
            {
                return Conflict(new { message = "Ese empleado ya tiene cuenta." });
            }
        }

        var user = new AppUser
        {
            Email = email,
            FullName = ResolveFullName(email, empleado),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = normalizedRole,
            IsActive = dto.IsActive,
            EmpleadoId = dto.EmpleadoId,
            MustChangePassword = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, MapUserResponse(user));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDto dto, CancellationToken ct = default)
    {
        if (dto is null)
        {
            return BadRequest(new { message = "Payload inválido." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return NotFound();
        }

        var normalizedRole = UserRoles.Normalize(dto.Role);
        if (!UserRoles.IsValid(normalizedRole))
        {
            return BadRequest(new
            {
                message = $"Rol inválido. Permitidos: {string.Join(", ", UserRoles.All)}"
            });
        }

        if (await WouldLeaveSystemWithoutAdminAsync(user, normalizedRole, dto.IsActive, ct))
        {
            return BadRequest(new
            {
                message = "No puedes dejar al sistema sin un ADMIN activo."
            });
        }

        Empleado? empleado = null;

        if (dto.EmpleadoId.HasValue)
        {
            empleado = await _db.Empleados.FirstOrDefaultAsync(
                e => e.Id == dto.EmpleadoId.Value, ct);

            if (empleado is null)
            {
                return BadRequest(new { message = "EmpleadoId no existe." });
            }

            var empleadoAlreadyLinked = await _db.Users.AnyAsync(
                u => u.Id != id && u.EmpleadoId == dto.EmpleadoId.Value, ct);

            if (empleadoAlreadyLinked)
            {
                return Conflict(new { message = "Ese empleado ya tiene cuenta." });
            }
        }

        user.Role = normalizedRole;
        user.IsActive = dto.IsActive;
        user.EmpleadoId = dto.EmpleadoId;
        user.UpdatedAtUtc = DateTime.UtcNow;

        var safeEmail = NormalizeEmail(user.Email);

        if (dto.EmpleadoId.HasValue && empleado is not null)
        {
            user.FullName = ResolveFullName(safeEmail, empleado);
        }
        else if (string.IsNullOrWhiteSpace(user.FullName))
        {
            user.FullName = ResolveFullName(safeEmail, null);
        }

        await _db.SaveChangesAsync(ct);

        return Ok(MapUserResponse(user));
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return NotFound();
        }

        var newPass = string.IsNullOrWhiteSpace(dto?.NewPassword)
            ? GenerateTempPassword()
            : dto.NewPassword!.Trim();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPass);
        user.MustChangePassword = true;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Password actualizado.",
            tempPassword = newPass
        });
    }

    [HttpPost("{id:int}/link-empleado-by-email")]
    public async Task<IActionResult> LinkEmpleadoByEmail(int id, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return NotFound(new { message = "Usuario no existe." });
        }

        var email = NormalizeEmail(user.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "El usuario no tiene Email válido." });
        }

        if (user.EmpleadoId.HasValue)
        {
            return Conflict(new { message = "Este usuario ya está ligado a un empleado." });
        }

        var empleado = await _db.Empleados.FirstOrDefaultAsync(
            e => e.Email != null && e.Email.ToLower() == email, ct);

        if (empleado is null)
        {
            return NotFound(new { message = "No existe empleado con el email del usuario." });
        }

        var empleadoAlreadyLinked = await _db.Users.AnyAsync(u => u.EmpleadoId == empleado.Id, ct);
        if (empleadoAlreadyLinked)
        {
            return Conflict(new { message = "Ese empleado ya tiene una cuenta ligada." });
        }

        user.EmpleadoId = empleado.Id;
        user.FullName = ResolveFullName(email, empleado);
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Empleado ligado al usuario por email.",
            user = MapUserResponse(user),
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

    private async Task<bool> WouldLeaveSystemWithoutAdminAsync(
        AppUser currentUser,
        string newRole,
        bool newIsActive,
        CancellationToken ct)
    {
        if (!UserRoles.IsAdmin(currentUser.Role))
        {
            return false;
        }

        var willStopBeingAdmin = !UserRoles.IsAdmin(newRole) || !newIsActive;
        if (!willStopBeingAdmin)
        {
            return false;
        }

        var otherActiveAdmins = await _db.Users.CountAsync(
            x => x.Id != currentUser.Id
              && x.IsActive
              && x.Role == UserRoles.Admin,
            ct);

        return otherActiveAdmins == 0;
    }

    private static object MapUserResponse(AppUser user)
    {
        return new
        {
            user.Id,
            user.Email,
            user.FullName,
            role = UserRoles.Normalize(user.Role),
            user.IsActive,
            user.MustChangePassword,
            user.EmpleadoId,
            user.CreatedAtUtc,
            user.UpdatedAtUtc
        };
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string ResolveFullName(string? email, Empleado? empleado)
    {
        if (empleado is not null)
        {
            var parts = new[]
            {
                empleado.Nombres?.Trim(),
                empleado.ApellidoPaterno?.Trim(),
                empleado.ApellidoMaterno?.Trim()
            }
            .Where(x => !string.IsNullOrWhiteSpace(x));

            var fullNameFromEmpleado = string.Join(" ", parts);
            if (!string.IsNullOrWhiteSpace(fullNameFromEmpleado))
            {
                return fullNameFromEmpleado;
            }
        }

        var safeEmail = (email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeEmail))
        {
            return string.Empty;
        }

        var localPart = safeEmail.Split('@')[0].Trim();
        return string.IsNullOrWhiteSpace(localPart) ? safeEmail : localPart;
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