using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Gv.Rh.Api.Services;

public class TokenService
{
    public const string EmpleadoIdClaimType = "empleado_id";

    private readonly IConfiguration _config;
    private readonly RhDbContext _db;
    private readonly AuditLogger _audit;

    public TokenService(IConfiguration config, RhDbContext db, AuditLogger audit)
    {
        _config = config;
        _db = db;
        _audit = audit;
    }

    public string CreateAccessToken(AppUser user)
    {
        var (_, issuer, audience, signingKey) = GetJwtConfig();

        var normalizedRole = UserRoles.Normalize(user.Role);
        var displayName = string.IsNullOrWhiteSpace(user.FullName)
            ? user.Email
            : user.FullName.Trim();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Role, normalizedRole),
            new("role", normalizedRole)
        };

        if (user.EmpleadoId.HasValue)
        {
            claims.Add(new Claim(
                EmpleadoIdClaimType,
                user.EmpleadoId.Value.ToString(),
                ClaimValueTypes.Integer32));
        }

        var jwt = _config.GetSection("Jwt");
        var accessMinutes = int.TryParse(jwt["AccessMinutes"], out var m) ? m : 15;
        var expires = DateTime.UtcNow.AddMinutes(accessMinutes);

        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(string refreshToken, string refreshTokenHash, DateTime expiresAtUtc)> CreateRefreshTokenAsync(
        AppUser user,
        bool saveChanges = true)
    {
        var (raw, hash, exp) = CreateRefreshTokenMaterial();
        var now = DateTime.UtcNow;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAtUtc = exp,
            CreatedAtUtc = now,
            ReplacedByTokenHash = null,
            RevokedAtUtc = null,
            RevokedReason = null
        });

        if (saveChanges)
        {
            await _db.SaveChangesAsync();
        }

        return (raw, hash, exp);
    }

    public async Task<(string accessToken, string refreshToken)> RotateRefreshTokenAsync(string refreshTokenPlain)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
        {
            throw new UnauthorizedAccessException("REFRESH_TOKEN_EMPTY");
        }

        var now = DateTime.UtcNow;
        var tokenHash = Hash(refreshTokenPlain);

        var rt = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.TokenHash == tokenHash &&
                x.RevokedAtUtc == null &&
                x.ReplacedByTokenHash == null);

        if (rt is null)
        {
            throw new UnauthorizedAccessException("REFRESH_TOKEN_INVALID");
        }

        if (rt.ExpiresAtUtc <= now)
        {
            rt.RevokedAtUtc = now;
            rt.RevokedReason = "EXPIRED";
            await _db.SaveChangesAsync();

            throw new UnauthorizedAccessException("REFRESH_TOKEN_EXPIRED");
        }

        var user = rt.User ?? throw new UnauthorizedAccessException("USER_NOT_FOUND");

        if (!user.IsActive)
        {
            rt.RevokedAtUtc = now;
            rt.RevokedReason = "USER_INACTIVE";
            await _db.SaveChangesAsync();

            throw new UnauthorizedAccessException("USER_INACTIVE");
        }

        if (user.MustChangePassword)
        {
            rt.RevokedAtUtc = now;
            rt.RevokedReason = "MUST_CHANGE_PASSWORD";
            await _db.SaveChangesAsync();

            throw new MustChangePasswordException();
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var (newRaw, newHash, newExp) = CreateRefreshTokenMaterial();

        rt.RevokedAtUtc = now;
        rt.RevokedReason = "ROTATED";
        rt.ReplacedByTokenHash = newHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAtUtc = newExp,
            CreatedAtUtc = now,
            RevokedAtUtc = null,
            RevokedReason = null,
            ReplacedByTokenHash = null
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _audit.LogAuthAsync("REFRESH", user, new { rotated = true });

        var access = CreateAccessToken(user);
        return (access, newRaw);
    }

    public async Task RevokeRefreshTokenAsync(string refreshTokenPlain)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var tokenHash = Hash(refreshTokenPlain);

        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        if (rt is null || rt.RevokedAtUtc != null)
        {
            return;
        }

        rt.RevokedAtUtc = now;
        rt.RevokedReason = "LOGOUT";
        await _db.SaveChangesAsync();
    }

    public async Task RevokeAllRefreshTokensForUserAsync(int userId)
    {
        var now = DateTime.UtcNow;

        var tokens = await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync();

        if (tokens.Count == 0)
        {
            return;
        }

        foreach (var t in tokens)
        {
            t.RevokedAtUtc = now;
            t.RevokedReason = "LOGOUT_ALL";
        }

        await _db.SaveChangesAsync();
    }

    public async Task<int> CleanupExpiredTokensAsync()
    {
        var now = DateTime.UtcNow;

        return await _db.RefreshTokens
            .Where(x => x.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync();
    }

    public ClaimsPrincipal ReadPrincipalFromExpiredToken(string token)
    {
        var (_, issuer, audience, signingKey) = GetJwtConfig();

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,

            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero,

            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtToken ||
            !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new SecurityTokenException("Token inválido.");
        }

        return principal;
    }

    private (string key, string issuer, string audience, SymmetricSecurityKey signingKey) GetJwtConfig()
    {
        var jwt = _config.GetSection("Jwt");

        var key = jwt["Key"] ?? throw new InvalidOperationException(
            "Falta Jwt:Key. Configúralo con User Secrets (DEV) o variables de entorno (PROD)."
        );

        var issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Falta Jwt:Issuer.");
        var audience = jwt["Audience"] ?? throw new InvalidOperationException("Falta Jwt:Audience.");

        if (key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        return (key, issuer, audience, signingKey);
    }

    private (string plain, string hash, DateTime expUtc) CreateRefreshTokenMaterial()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var raw = Convert.ToBase64String(bytes);
        var hash = Hash(raw);

        var refreshDays = int.TryParse(_config["Jwt:RefreshDays"], out var d) ? d : 14;
        var exp = DateTime.UtcNow.AddDays(refreshDays);

        return (raw, hash, exp);
    }

    private static string Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}