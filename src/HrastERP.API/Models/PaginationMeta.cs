namespace HrastERP.API.Models;

public record PaginationMeta(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);
