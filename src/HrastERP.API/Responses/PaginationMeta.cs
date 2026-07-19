namespace HrastERP.API.Responses;

public record PaginationMeta(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);
