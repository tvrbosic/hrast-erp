namespace HrastERP.SharedKernel.Common;

/// <summary>
/// One page of a list query's results; create it with <see cref="Create"/>.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; }
    /// <summary>Number of items across all pages, not just this one.</summary>
    public int TotalCount { get; }

    /// <summary>1-based page number.</summary>
    public int Page { get; }
    public int PageSize { get; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    private PagedResult(IReadOnlyCollection<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public static PagedResult<T> Create(IEnumerable<T> items, int totalCount, int page, int pageSize)
        => new(items.ToList().AsReadOnly(), totalCount, page, pageSize);
}
