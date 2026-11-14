using Microsoft.AspNetCore.Authorization;

namespace HrastERP.API.Authorization;

// Marker requirement: "the user must belong to the super admin tenant."
// SuperAdminTenantAuthorizationHandler receives it and performs the check.
public sealed class SuperAdminTenantRequirement : IAuthorizationRequirement;
