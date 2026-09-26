using System.Net;
using System.Security.Claims;
using System.Text.Json;
using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Caching;
using HrastERP.Infrastructure.Database;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;

namespace HrastERP.API.Middleware;

public class BlockInactiveEntityMiddleware(
    RequestDelegate next,
    ICacheService cacheService,
    IServiceScopeFactory scopeFactory,
    ILogger<BlockInactiveEntityMiddleware> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        // Pass through if not authenticated
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        // Extract tenant and user IDs from JWT claims
        if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId) ||
            !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await next(context);
            return;
        }

        // Super admin tenant is never blocked
        if (tenantId == TenantConstants.SuperAdminTenantId)
        {
            await next(context);
            return;
        }

        // Check tenant active status
        var tenantActive = await GetTenantActiveStatusAsync(tenantId);
        if (tenantActive == false)
        {
            logger.LogWarning("Blocked request from inactive tenant {TenantId} by user {UserId}", tenantId, userId);
            await WriteForbiddenResponseAsync(context);
            return;
        }

        // Check user active status
        var userActive = await GetUserActiveStatusAsync(userId);
        if (userActive == false)
        {
            logger.LogWarning("Blocked request from inactive user {UserId} in tenant {TenantId}", userId, tenantId);
            await WriteForbiddenResponseAsync(context);
            return;
        }

        await next(context);
    }

    private async Task<bool?> GetTenantActiveStatusAsync(Guid tenantId)
    {
        var cacheKey = $"Tenants:active:{tenantId}";
        var cached = await cacheService.GetAsync<bool?>(cacheKey);

        if (cached.HasValue)
            return cached.Value;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HrastDbContext>();

        var tenant = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId && t.DeletedAt == null)
            .Select(t => (bool?)t.IsActive)
            .FirstOrDefaultAsync();

        if (tenant.HasValue)
            await cacheService.SetAsync(cacheKey, tenant.Value, CacheTtl);

        return tenant;
    }

    private async Task<bool?> GetUserActiveStatusAsync(Guid userId)
    {
        var cacheKey = $"Users:active:{userId}";
        var cached = await cacheService.GetAsync<bool?>(cacheKey);

        if (cached.HasValue)
            return cached.Value;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HrastDbContext>();

        var isActive = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.IsActive)
            .FirstOrDefaultAsync();

        if (isActive.HasValue)
            await cacheService.SetAsync(cacheKey, isActive.Value, CacheTtl);

        return isActive;
    }

    private static async Task WriteForbiddenResponseAsync(HttpContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
        context.Response.ContentType = "application/json";

        var body = new ErrorResponse(
            "General.InactiveEntity",
            "Your account or tenant has been deactivated. Contact your administrator.");
        await context.Response.WriteAsJsonAsync(body, JsonOptions);
    }
}
