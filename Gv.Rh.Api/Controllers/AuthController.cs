using Gv.Rh.Api.Services;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        var email = (req.Email ?? "").Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email);
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Credenciales inválidas." });

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Credenciales inválidas." });

        var access = _tokens.CreateAccessToken(user);

        // ✅ Auditar login exitoso (sin secretos)
        await _audit.LogAuthAsync("LOGIN", user, new { mustChangePassword = user.MustChangePassword });

        // ✅ Si debe cambiar password, NO emitimos refresh token
        if (user.MustChangePassword)
        {
            return Ok(new
            {
                accessToken = access,
                refreshToken = (string?)null,
                refreshExpiresAtUtc = (DateTime?)null,
                mustChangePassword = true
            });
        }

        var (refresh, _, exp) = await _tokens.CreateRefreshTokenAsync(user);

        return Ok(new
        {
            accessToken = access,
            refreshToken = refresh,
            refreshExpiresAtUtc = exp,
            mustChangePassword = false
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        try
        {
            // Nota: el TokenService ya audita "REFRESH" al rotar
            var (access, refresh) = await _tokens.RotateRefreshTokenAsync(req.RefreshToken);
            return Ok(new { accessToken = access, refreshToken = refresh });
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

    // Revocar todos los refresh tokens del usuario autenticado
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(sub, out var userId))
            return Unauthorized();

        await _tokens.RevokeAllRefreshTokensForUserAsync(userId);

        await _audit.LogAuthAsync("LOGOUT_ALL", data: new { ok = true });

        return NoContent();
    }

    [Authorize]
    [HttpGet("whoami")]
    public IActionResult WhoAmI()
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email);
        var role = User.FindFirstValue(ClaimTypes.Role);

        return Ok(new { userId, email, role });
    }

    // Cambiar password (apaga MustChangePassword)
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(sub, out var userId))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null || !user.IsActive)
            return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return Unauthorized(new { message = "Password actual incorrecto." });

        var newPass = (req.NewPassword ?? "").Trim();
        if (newPass.Length < 10)
            return BadRequest(new { message = "El nuevo password debe tener al menos 10 caracteres." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPass);
        user.MustChangePassword = false;

        // 🔥 al cambiar password, revoca todos los refresh tokens
        await _tokens.RevokeAllRefreshTokensForUserAsync(user.Id);

        await _db.SaveChangesAsync();

        await _audit.LogAuthAsync("PASSWORD_CHANGE", user, new { revokeAll = true });

        return NoContent();
    }
}