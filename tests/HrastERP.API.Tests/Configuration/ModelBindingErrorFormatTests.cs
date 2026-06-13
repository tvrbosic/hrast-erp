using FluentAssertions;
using HrastERP.API.Extensions;
using HrastERP.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HrastERP.API.Tests.Configuration;

public class ModelBindingErrorFormatTests
{
    private static IActionResult InvokeFactory(ModelStateDictionary modelState)
    {
        var services = new ServiceCollection();
        services.AddControllers();
        services.ConfigureModelBindingErrorFormat();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;

        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor(),
            modelState);

        return options.InvalidModelStateResponseFactory(actionContext);
    }

    [Fact]
    public void Returns_422_UnprocessableEntity()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The Email field is required.");

        var result = InvokeFactory(modelState);

        var objectResult = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);
    }

    [Fact]
    public void Response_body_uses_ErrorResponse_format()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The Email field is required.");

        var result = InvokeFactory(modelState);

        var objectResult = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        var body = objectResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        body.Code.Should().Be("General.Validation");
        body.Message.Should().Be("One or more validation errors occurred.");
    }

    [Fact]
    public void Field_names_are_camelCase()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("FirstName", "The FirstName field is required.");

        var result = InvokeFactory(modelState);

        var body = (result as UnprocessableEntityObjectResult)!.Value as ErrorResponse;
        body!.Errors.Should().ContainKey("firstName");
        body.Errors.Should().NotContainKey("FirstName");
    }

    [Fact]
    public void Multiple_errors_on_same_field_are_grouped()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Password", "Password is required.");
        modelState.AddModelError("Password", "Password must be at least 8 characters.");

        var result = InvokeFactory(modelState);

        var body = (result as UnprocessableEntityObjectResult)!.Value as ErrorResponse;
        body!.Errors!["password"].Should().HaveCount(2);
        body.Errors["password"].Should().Contain("Password is required.");
        body.Errors["password"].Should().Contain("Password must be at least 8 characters.");
    }

    [Fact]
    public void Multiple_fields_each_get_their_own_entry()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The Email field is required.");
        modelState.AddModelError("Password", "The Password field is required.");

        var result = InvokeFactory(modelState);

        var body = (result as UnprocessableEntityObjectResult)!.Value as ErrorResponse;
        body!.Errors.Should().HaveCount(2);
        body.Errors.Should().ContainKey("email");
        body.Errors.Should().ContainKey("password");
    }
}
