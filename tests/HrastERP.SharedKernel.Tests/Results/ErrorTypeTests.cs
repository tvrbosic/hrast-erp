using FluentAssertions;
using HrastERP.SharedKernel.Results;

namespace HrastERP.SharedKernel.Tests.Results;

public class ErrorTypeTests
{
    [Fact]
    public void Contains_all_expected_members_When_enum_is_defined()
    {
        var values = Enum.GetValues<ErrorType>();

        values.Should().Contain(ErrorType.Validation);
        values.Should().Contain(ErrorType.NotFound);
        values.Should().Contain(ErrorType.Forbidden);
        values.Should().Contain(ErrorType.Conflict);
        values.Should().Contain(ErrorType.Unexpected);
    }
}
