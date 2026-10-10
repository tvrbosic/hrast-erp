using FluentAssertions;
using HrastERP.SharedKernel.Common;

namespace HrastERP.SharedKernel.Tests.Common;

public class PagedResultTests
{
    [Fact]
    public void Sets_all_properties_When_created()
    {
        var items = new[] { 1, 2, 3 };
        var result = PagedResult<int>.Create(items, totalCount: 10, page: 1, pageSize: 3);

        result.Items.Should().BeEquivalentTo(items);
        result.TotalCount.Should().Be(10);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);
    }

    [Fact]
    public void Rounds_TotalPages_up_When_items_do_not_divide_evenly()
    {
        var result = PagedResult<int>.Create([], totalCount: 10, page: 1, pageSize: 3);

        result.TotalPages.Should().Be(4);
    }

    [Fact]
    public void Returns_exact_TotalPages_When_items_divide_evenly()
    {
        var result = PagedResult<int>.Create([], totalCount: 9, page: 1, pageSize: 3);

        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public void HasPreviousPage_is_false_When_on_first_page()
    {
        var result = PagedResult<int>.Create([], totalCount: 10, page: 1, pageSize: 3);

        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_is_true_When_past_first_page()
    {
        var result = PagedResult<int>.Create([], totalCount: 10, page: 2, pageSize: 3);

        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_is_false_When_on_last_page()
    {
        var result = PagedResult<int>.Create([], totalCount: 9, page: 3, pageSize: 3);

        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasNextPage_is_true_When_before_last_page()
    {
        var result = PagedResult<int>.Create([], totalCount: 10, page: 1, pageSize: 3);

        result.HasNextPage.Should().BeTrue();
    }
}
