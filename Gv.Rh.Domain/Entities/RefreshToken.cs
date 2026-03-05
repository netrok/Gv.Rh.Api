namespace Gv.Rh.Domain.Entities;

public class RefreshToken
{
    public long Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = default!;

    // SHA256 base64 (~44 chars)
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAtUtc { get; set; }

    // ROTATED, LOGOUT, LOGOUT_ALL, EXPIRED, USER_INACTIVE, MUST_CHANGE_PASSWORD
    public string? RevokedReason { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
}