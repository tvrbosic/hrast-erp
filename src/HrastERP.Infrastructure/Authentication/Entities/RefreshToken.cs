using HrastERP.SharedKernel.Domain;

namespace HrastERP.Infrastructure.Authentication;

public sealed class RefreshToken : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    /// <summary>SHA-256 hash of the raw token; the raw token is only ever sent to the client.</summary>
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    /// <summary>Hash of the token that replaced this one when it was rotated.</summary>
    public string? ReplacedByToken { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive => !IsExpired && !IsRevoked;

    public ApplicationUser User { get; set; } = null!;
}
