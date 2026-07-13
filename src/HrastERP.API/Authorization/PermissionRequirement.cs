using HrastERP.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace HrastERP.API.Authorization;

// Data-carrier that expresses what must be satisfied: "the user must have this Permission flag."
// Created by PermissionPolicyProvider when it builds a policy from a "Permission:<long>" name.
// PermissionAuthorizationHandler receives it and performs the actual bitwise check.
public sealed class PermissionRequirement(Permission permission) : IAuthorizationRequirement
{
    public Permission Permission { get; } = permission;
}
