using FluentAssertions;
using HrastERP.API.Extensions;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Common;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Tests.Extensions;

public class ResultExtensionsTests
{
    [Fact]
    public void Returns_NoContentResult_When_non_generic_result_succeeds()
    {
        var actionResult = Result.Success().ToActionResult();

        actionResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void Returns_OkObjectResult_with_envelope_When_generic_result_succeeds()
    {
        var actionResult = Result<string>.Success("x").ToActionResult();

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should().BeOfType<ApiResponse<string>>().Subject;
        envelope.Data.Should().Be("x");
        envelope.Meta.Should().BeNull();
    }

    [Fact]
    public void Returns_paged_envelope_with_meta_When_PagedResult_succeeds()
    {
        var paged = PagedResult<int>.Create([1, 2], totalCount: 5, page: 1, pageSize: 2);

        var actionResult = Result<PagedResult<int>>.Success(paged).ToActionResult();

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should().BeOfType<ApiResponse<IReadOnlyCollection<int>>>().Subject;
        envelope.Data.Should().Equal(1, 2);
        var meta = envelope.Meta.Should().BeOfType<PaginationMeta>().Subject;
        meta.TotalCount.Should().Be(5);
        meta.TotalPages.Should().Be(3);
        meta.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Returns_CreatedAtActionResult_with_envelope_When_create_result_succeeds()
    {
        var id = Guid.NewGuid();

        var actionResult = Result<Guid>.Success(id).ToCreatedResult("GetById");

        var created = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(201);
        created.ActionName.Should().Be("GetById");
        created.ControllerName.Should().BeNull();
        created.RouteValues!["id"].Should().Be(id);
        created.Value.Should().BeOfType<ApiResponse<Guid>>().Which.Data.Should().Be(id);
    }

    [Fact]
    public void Returns_error_result_When_create_result_fails()
    {
        var result = Result<Guid>.Failure(Error.Conflict("Tenant.NameTaken", "taken"));

        var actionResult = result.ToCreatedResult("GetById");

        var conflict = actionResult.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeOfType<ErrorResponse>().Which.Code.Should().Be("Tenant.NameTaken");
    }

    [Theory]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unexpected, 500)]
    [InlineData(ErrorType.Validation, 422)]
    public void Returns_mapped_status_When_result_fails(ErrorType type, int expectedStatus)
    {
        var error = type switch
        {
            ErrorType.NotFound => Error.NotFound("Test.Code", "m"),
            ErrorType.Forbidden => Error.Forbidden("Test.Code", "m"),
            ErrorType.Conflict => Error.Conflict("Test.Code", "m"),
            ErrorType.Unexpected => Error.Unexpected("Test.Code", "m"),
            _ => Error.Validation("Test.Code", "m")
        };

        var actionResult = Result.Failure(error).ToActionResult();

        var objectResult = actionResult.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);
        objectResult.Value.Should().BeOfType<ErrorResponse>().Which.Code.Should().Be("Test.Code");
    }

    [Fact]
    public void Includes_field_errors_When_validation_result_fails()
    {
        var fieldErrors = new Dictionary<string, string[]> { ["name"] = ["required"] };

        var actionResult = Result.Failure(Error.Validation("General.Validation", "m", fieldErrors)).ToActionResult();

        var objectResult = actionResult.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.Value.Should().BeOfType<ErrorResponse>()
            .Which.Errors!["name"].Should().Equal("required");
    }
}
