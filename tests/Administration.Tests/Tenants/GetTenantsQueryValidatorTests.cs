using FluentValidation.TestHelper;
using HrastERP.Administration.Application.Tenants.Queries.GetTenants;

namespace Administration.Tests.Tenants;

public class GetTenantsQueryValidatorTests
{
    private readonly GetTenantsQueryValidator _sut = new();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(50, 10)]
    public void Passes_When_page_and_page_size_are_in_range(int page, int pageSize)
    {
        _sut.TestValidate(new GetTenantsQuery(page, pageSize, null)).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fails_on_page_When_page_is_not_positive(int page)
    {
        _sut.TestValidate(new GetTenantsQuery(page, 10, null)).ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public void Fails_on_page_size_When_page_size_is_out_of_range(int pageSize)
    {
        _sut.TestValidate(new GetTenantsQuery(1, pageSize, null)).ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Fails_on_page_When_offset_would_overflow()
    {
        _sut.TestValidate(new GetTenantsQuery(int.MaxValue, 100, null)).ShouldHaveValidationErrorFor(x => x.Page);
    }
}
