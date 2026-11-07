using HrastERP.Infrastructure.Authorization;
using HrastERP.SharedKernel.Domain;
using Microsoft.AspNetCore.Identity;

namespace HrastERP.Infrastructure.Authentication;

public sealed class ApplicationUser : IdentityUser<Guid>, ITenantEntity, IAuditable, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public Guid? RoleId { get; set; }
    public Role? Role { get; set; }

    // IAuditable
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // ISoftDeletable
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
}
