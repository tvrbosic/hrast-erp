using FluentAssertions;
using FluentValidation;
using HrastERP.Administration;
using HrastERP.Administration.Application.Roles.Commands.CreateRole;
using HrastERP.Administration.Application.Roles.Commands.UpdateRole;
using HrastERP.Administration.Application.Tenants.Commands.CreateTenant;
using HrastERP.Administration.Application.Tenants.Commands.UpdateTenant;
using HrastERP.Administration.Application.Users.Commands.ChangePassword;
using HrastERP.Administration.Application.Users.Commands.CreateUser;
using HrastERP.Administration.Application.Users.Commands.UpdateUser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Administration.Tests;

public class AdministrationModuleTests
{
    public static TheoryData<Type> ValidatedCommands =>
    [
        typeof(CreateTenantCommand),
        typeof(UpdateTenantCommand),
        typeof(CreateUserCommand),
        typeof(UpdateUserCommand),
        typeof(ChangePasswordCommand),
        typeof(CreateRoleCommand),
        typeof(UpdateRoleCommand),
    ];

    [Theory]
    [MemberData(nameof(ValidatedCommands))]
    public void Registers_internal_validator_When_module_is_added(Type commandType)
    {
        var services = new ServiceCollection();
        services.AddAdministrationModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var validatorType = typeof(IValidator<>).MakeGenericType(commandType);
        var validators = provider.GetServices(validatorType);

        validators.Should().ContainSingle();
    }
}
