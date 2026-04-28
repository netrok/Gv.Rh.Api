using Gv.Rh.Api.Services;
using Gv.Rh.Domain.Common;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly TokenService _tokens;
    private readonly AuditLogger _audit;

    public AuthController(RhDbContext db, TokenService tokens, AuditLogger audit)
    {
        _db = db;
        _tokens = tokens;
        _audit = audit;
    }

    public record LoginRequest(string Email, string Password);
    public record RefreshRequest(string RefreshToken);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        user.Role = UserRoles.Normalize(user.Role);

        var access = _tokens.CreateAccessToken(user);

        await _audit.LogAuthAsync("LOGIN", user, new
        {
            mustChangePassword = user.MustChangePassword,
            empleadoId = user.EmpleadoId
        });

        if (user.MustChangePassword)
        {
            return Ok(new
            {
                accessToken = access,
                refreshToken = (string?)null,
                refreshExpiresAtUtc = (DateTime?)null,
                mustChangePassword = true,
                user = BuildUserPayload(user)
            });
        }

        var (refresh, _, exp) = await _tokens.CreateRefreshTokenAsync(user);

        return Ok(new
        {
            accessToken = access,
            refreshToken = refresh,
            refreshExpiresAtUtc = exp,
            mustChangePassword = false,
            user = BuildUserPayload(user)
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        try
        {
            var (access, refresh) = await _tokens.RotateRefreshTokenAsync(req.RefreshToken);

            var principal = _tokens.ReadPrincipalFromExpiredToken(access);
            var userIdValue = GetUserIdClaimValue(principal);

            if (!int.TryParse(userIdValue, out var userId))
            {
                return Unauthorized(new { message = "Token inválido." });
            }

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
            if (user is null || !user.IsActive)
            {
                return Unauthorized(new { message = "Usuario no válido." });
            }

            return Ok(new
            {
                accessToken = access,
                refreshToken = refresh,
                user = BuildUserPayload(user)
            });
        }
        catch (MustChangePasswordException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Debes cambiar tu contraseña antes de usar el sistema.",
                code = "MUST_CHANGE_PASSWORD"
            });
        }
        catch
        {
            return Unauthorized(new { message = "Refresh token inválido o expirado." });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        await _tokens.RevokeRefreshTokenAsync(req.RefreshToken);

        await _audit.LogAuthAsync("LOGOUT", data: new { ok = true });

        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var userIdValue = GetUserIdClaimValue(User);

        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        await _tokens.RevokeAllRefreshTokensForUserAsync(userId);

        await _audit.LogAuthAsync("LOGOUT_ALL", data: new { ok = true });

        return NoContent();
    }

    [Authorize]
    [HttpGet("whoami")]
    public IActionResult WhoAmI()
    {
        var userId = GetUserIdClaimValue(User);
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email");
        var fullName = User.FindFirstValue(ClaimTypes.Name);
        var role = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role");
        var empleadoId = User.FindFirstValue(TokenService.EmpleadoIdClaimType);

        return Ok(new
        {
            userId,
            email,
            fullName,
            role,
            empleadoId
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userIdValue = GetUserIdClaimValue(User);

        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
        {
            return Unauthorized(new { message = "Password actual incorrecto." });
        }

        var newPass = (req.NewPassword ?? string.Empty).Trim();
        if (newPass.Length < 10)
        {
            return BadRequest(new { message = "El nuevo password debe tener al menos 10 caracteres." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPass);
        user.MustChangePassword = false;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _tokens.RevokeAllRefreshTokensForUserAsync(user.Id);
        await _db.SaveChangesAsync();

        await _audit.LogAuthAsync("PASSWORD_CHANGE", user, new
        {
            revokeAll = true,
            empleadoId = user.EmpleadoId
        });

        return NoContent();
    }

    private static string? GetUserIdClaimValue(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
    }

    private static object BuildUserPayload(dynamic user)
    {
        return new
        {
            id = user.Id,
            email = user.Email,
            fullName = user.FullName,
            role = UserRoles.Normalize(user.Role),
            isActive = user.IsActive,
            mustChangePassword = user.MustChangePassword,
            empleadoId = user.EmpleadoId
        };
    }
}