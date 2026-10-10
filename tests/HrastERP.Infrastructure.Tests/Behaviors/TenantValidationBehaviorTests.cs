using FluentAssertions;
using HrastERP.Infrastructure.Behaviors;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Results;
using MediatR;
using NSubstitute;

namespace HrastERP.Infrastructure.Tests.Behaviors;

public class TenantValidationBehaviorTests
{
    private sealed record TestRequest : IRequest<Result>;

    private sealed record TestRequestWithValue : IRequest<Result<string>>;

    [Fact]
    public async Task Returns_forbidden_failure_When_TenantId_is_empty()
    {
        var tenant = Substitute.For<ICurrentTenant>();
        var behavior = new TenantValidationBehavior<TestRequest, Result>(tenant);
        RequestHandlerDelegate<Result> next = _ => Task.FromResult(Result.Success());

        var result = await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("General.MissingTenant");
    }

    [Fact]
    public async Task Returns_forbidden_failure_for_ResultT_When_TenantId_is_empty()
    {
        var tenant = Substitute.For<ICurrentTenant>();
        var behavior = new TenantValidationBehavior<TestRequestWithValue, Result<string>>(tenant);
        RequestHandlerDelegate<Result<string>> next = _ => Task.FromResult(Result<string>.Success("ok"));

        var result = await behavior.Handle(new TestRequestWithValue(), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("General.MissingTenant");
    }

    [Fact]
    public async Task Calls_next_When_TenantId_is_valid()
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.TenantId.Returns(Guid.NewGuid());
        var behavior = new TenantValidationBehavior<TestRequest, Result>(tenant);
        var nextCalled = false;
        RequestHandlerDelegate<Result> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        var result = await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Calls_next_for_ResultT_When_TenantId_is_valid()
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.TenantId.Returns(Guid.NewGuid());
        var behavior = new TenantValidationBehavior<TestRequestWithValue, Result<string>>(tenant);
        var nextCalled = false;
        RequestHandlerDelegate<Result<string>> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result<string>.Success("ok"));
        };

        var result = await behavior.Handle(new TestRequestWithValue(), next, CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }
}
