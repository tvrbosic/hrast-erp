using FluentAssertions;
using HrastERP.API.Extensions;
using HrastERP.API.Responses;
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
    public void Returns_422_When_model_state_is_invalid()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The Email field is required.");

        var result = InvokeFactory(modelState);

        var objectResult = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);
    }

    [Fact]
    public void Returns_ErrorResponse_format_When_model_state_is_invalid()
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
    public void Converts_field_names_to_camelCase_When_building_error_response()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("FirstName", "The FirstName field is required.");

        var result = InvokeFactory(modelState);

        var body = (result as UnprocessableEntityObjectResult)!.Value as ErrorResponse;
        body!.Errors.Should().ContainKey("firstName");
        body.Errors.Should().NotContainKey("FirstName");
    }

    [Fact]
    public void Groups_errors_under_same_field_When_multiple_errors_exist()
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
    public void Creates_separate_entry_for_each_field_When_multiple_fields_have_errors()
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
