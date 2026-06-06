using FluentAssertions;
using HrastERP.SharedKernel.Results;

namespace HrastERP.SharedKernel.Tests.Results;

public class ErrorTypeTests
{
    [Fact]
    public void ErrorType_has_expected_members()
    {
        var values = Enum.GetValues<ErrorType>();

        values.Should().Contain(ErrorType.Validation);
        values.Should().Contain(ErrorType.NotFound);
        values.Should().Contain(ErrorType.Forbidden);
        values.Should().Contain(ErrorType.Conflict);
        values.Should().Contain(ErrorType.Unexpected);
    }
}
