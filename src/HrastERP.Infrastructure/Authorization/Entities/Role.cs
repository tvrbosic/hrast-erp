using HrastERP.Infrastructure.Authentication;
using HrastERP.SharedKernel.Authorization;

namespace HrastERP.Infrastructure.Authorization;

public sealed class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Permission Permissions { get; set; }

    public ICollection<ApplicationUser> Users { get; set; } = [];
}
