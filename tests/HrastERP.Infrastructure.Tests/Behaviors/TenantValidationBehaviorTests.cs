using FluentAssertions;
using HrastERP.Infrastructure.Behaviors;
using HrastERP.SharedKernel.Abstractions;
using HrastERP.SharedKernel.Results;
using MediatR;

namespace HrastERP.Infrastructure.Tests.Behaviors;

public class TenantValidationBehaviorTests
{
    private sealed class FakeCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
    }

    private sealed record TestRequest : IRequest<Result>;

    private sealed record TestRequestWithValue : IRequest<Result<string>>;

    [Fact]
    public async Task Returns_forbidden_failure_when_TenantId_is_empty()
    {
        var behavior = new TenantValidationBehavior<TestRequest, Result>(new FakeCurrentTenant(Guid.Empty));
        RequestHandlerDelegate<Result> next = _ => Task.FromResult(Result.Success());

        var result = await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("General.MissingTenant");
    }

    [Fact]
    public async Task Returns_forbidden_failure_for_Result_T_when_TenantId_is_empty()
    {
        var behavior = new TenantValidationBehavior<TestRequestWithValue, Result<string>>(new FakeCurrentTenant(Guid.Empty));
        RequestHandlerDelegate<Result<string>> next = _ => Task.FromResult(Result<string>.Success("ok"));

        var result = await behavior.Handle(new TestRequestWithValue(), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("General.MissingTenant");
    }

    [Fact]
    public async Task Calls_next_when_TenantId_is_valid()
    {
        var behavior = new TenantValidationBehavior<TestRequest, Result>(new FakeCurrentTenant(Guid.NewGuid()));
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
    public async Task Calls_next_for_Result_T_when_TenantId_is_valid()
    {
        var behavior = new TenantValidationBehavior<TestRequestWithValue, Result<string>>(new FakeCurrentTenant(Guid.NewGuid()));
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
