using System.Reflection;
using FluentAssertions;
using HrastERP.API.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Tests.Controllers;

// Create actions return ToCreatedResult(nameof(GetById)), which puts the new id into GetById's {id} route
// value; the Location URL is only built when the response is written. These tests pin the route shape that
// call relies on, so a change
// to GetById fails here instead of turning every create into a 500 after the row is saved.
public class CreatedLocationRouteTests
{
    [Theory]
    [InlineData(typeof(RolesController))]
    [InlineData(typeof(TenantsController))]
    [InlineData(typeof(UsersController))]
    public void Has_GetById_with_guid_id_route_When_controller_creates_resources(Type controller)
    {
        var getById = controller.GetMethod("GetById", BindingFlags.Public | BindingFlags.Instance);

        getById.Should().NotBeNull();
        getById!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("{id:guid}");
        getById.GetParameters().Should().Contain(p => p.Name == "id" && p.ParameterType == typeof(Guid));
    }
}
