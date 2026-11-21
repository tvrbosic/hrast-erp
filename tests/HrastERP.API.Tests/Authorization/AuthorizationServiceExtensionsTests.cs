using FluentAssertions;
using HrastERP.API.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.API.Tests.Authorization;

public class AuthorizationServiceExtensionsTests
{
    private static IServiceCollection CreateServices() =>
        new ServiceCollection().AddLogging().AddApiAuthorization();

    [Fact]
    public void Registers_permission_policy_provider_as_singleton_When_AddApiAuthorization_is_called()
    {
        var services = CreateServices();

        // The last registration wins, so it must be PermissionPolicyProvider, not the framework default
        services.Last(d => d.ServiceType == typeof(IAuthorizationPolicyProvider))
            .Should().Match<ServiceDescriptor>(d =>
                d.ImplementationType == typeof(PermissionPolicyProvider)
                && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Registers_authorization_handlers_as_scoped_When_AddApiAuthorization_is_called()
    {
        var handlers = CreateServices()
            .Where(d => d.ServiceType == typeof(IAuthorizationHandler)
                        && (d.ImplementationType == typeof(PermissionAuthorizationHandler)
                            || d.ImplementationType == typeof(SuperAdminTenantAuthorizationHandler)))
            .ToList();

        handlers.Should().HaveCount(2)
            .And.OnlyContain(d => d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Registers_forbidden_response_handler_as_singleton_When_AddApiAuthorization_is_called()
    {
        CreateServices().Should().Contain(d =>
            d.ServiceType == typeof(IAuthorizationMiddlewareResultHandler)
            && d.ImplementationType == typeof(ForbiddenResponseHandler)
            && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task Resolves_super_admin_tenant_policy_When_AddApiAuthorization_is_called()
    {
        using var provider = CreateServices().BuildServiceProvider();

        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(RequireSuperAdminTenantAttribute.PolicyName);

        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle(r => r is SuperAdminTenantRequirement);
    }
}
