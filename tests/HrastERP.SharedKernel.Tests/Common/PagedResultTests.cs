using FluentAssertions;
using HrastERP.SharedKernel.Common;

namespace HrastERP.SharedKernel.Tests.Common;

public class PagedResultTests
{
    [Fact]
    public void Create_sets_items_totalCount_page_and_pageSize()
    {
        var items = new[] { 1, 2, 3 };
        var result = PagedResult<int>.Create(items, totalCount: 10, page: 1, pageSize: 3);

        result.Items.Should().BeEquivalentTo(items);
        result.TotalCount.Should().Be(10);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);
    }

    [Fact]
    public void TotalPages_rounds_up()
    {
        var result = PagedResult<int>.Create([], totalCount: 10, page: 1, pageSize: 3);

        result.TotalPages.Should().Be(4);
    }

    [Fact]
    public void TotalPages_is_exact_when_evenly_divisible()
    {
        var result = PagedResult<int>.Create([], totalCount: 9, page: 1, pageSize: 3);

        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public void HasPreviousPage_is_false_on_first_page()
    {
        var result = PagedResult<int>.Create([], totalCount: 10, page: 1, pageSize: 3);

        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_is_true_after_first_page()
    {
        var result = PagedResult<int>.Create([], totalCount: 10, page: 2, pageSize: 3);

        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_is_false_on_last_page()
    {
        var result = PagedResult<int>.Create([], totalCount: 9, page: 3, pageSize: 3);

        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasNextPage_is_true_before_last_page()
    {
        var result = PagedResult<int>.Create([], totalCount: 10, page: 1, pageSize: 3);

        result.HasNextPage.Should().BeTrue();
    }
}
