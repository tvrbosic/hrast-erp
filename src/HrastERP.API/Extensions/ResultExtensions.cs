using HrastERP.API.Responses;
using HrastERP.SharedKernel.Common;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Extensions;

/// <summary>
/// Turns a handler's <see cref="Result"/> into the API's response shapes and picks the success status:
/// <c>201 Created</c> for creates (<see cref="ToCreatedResult"/>), <c>204 No Content</c> for a
/// <see cref="Result"/> without a value, and <c>200 OK</c> otherwise. Success bodies are wrapped as
/// <c>{ "data": T }</c> (<see cref="ApiResponse{T}"/>); failures become a flat <see cref="ErrorResponse"/>
/// with the HTTP status chosen by <see cref="ErrorType"/>.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Returns <c>204 No Content</c> on success, otherwise the mapped error response.
    /// </summary>
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new NoContentResult();

        return ToErrorResult(result.Error);
    }

    /// <summary>
    /// Returns <c>200 OK</c> with the value wrapped as <c>{ "data": T }</c> on success, otherwise the mapped
    /// error response. A <see cref="PagedResult{T}"/> value is unwrapped to its items, with the paging
    /// details added as <c>"meta"</c>.
    /// </summary>
    public static IActionResult ToActionResult<TValue>(this Result<TValue> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(WrapInEnvelope(result.Value));

        return ToErrorResult(result.Error);
    }

    /// <summary>
    /// Returns <c>201 Created</c> for a create whose result is the id of the newly created entity, otherwise
    /// the mapped error response. The body is <c>{ "data": id }</c> and the <c>Location</c> header is the URL
    /// of <paramref name="getByIdAction"/> on the current controller for that id, e.g. <c>/api/admin/tenants/{id}</c>.
    /// </summary>
    /// <param name="result">The create handler's result, holding the id of the entity it saved.</param>
    /// <param name="getByIdAction">
    /// The action that returns one entity by id, passed as <c>nameof(GetById)</c>. Its route must have an
    /// <c>{id}</c> parameter.
    /// </param>
    public static IActionResult ToCreatedResult(this Result<Guid> result, string getByIdAction)
    {
        if (result.IsSuccess)
        {
            var createdEntityGuid = result.Value;

            return new CreatedAtActionResult(
                getByIdAction,
                // Which controller the Location URL points to. This method is shared by every controller, so it
                // doesn't know which one called it. Passing null tells ASP.NET Core to use the controller of the
                // current request: for POST api/admin/tenants that is TenantsController, so the URL points at
                // TenantsController.GetById
                controllerName: null,
                // Route values fill the placeholders in getByIdAction's route: { id = createdEntityGuid } turns
                // "{id:guid}" into the new entity's URL for the Location header. The property must be named "id"
                // to match the route, or building the URL fails with a 500 after the entity is already saved
                routeValues: new { id = createdEntityGuid },
                value: WrapInEnvelope(createdEntityGuid));
        }

        return ToErrorResult(result.Error);
    }

    private static object WrapInEnvelope<TValue>(TValue value)
    {
        var type = typeof(TValue);

        // The item type is only known at runtime, so the dynamic call lets the runtime pick
        // WrapPagedResult<TItem> for the actual PagedResult<TItem>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PagedResult<>))
            return WrapPagedResult((dynamic)value!);

        return new ApiResponse<TValue>(value);
    }

    private static ApiResponse<IReadOnlyCollection<TItem>> WrapPagedResult<TItem>(PagedResult<TItem> paged)
    {
        return new ApiResponse<IReadOnlyCollection<TItem>>(paged.Items)
        {
            Meta = new PaginationMeta(
                paged.Page, paged.PageSize, paged.TotalCount,
                paged.TotalPages, paged.HasPreviousPage, paged.HasNextPage)
        };
    }

    private static IActionResult ToErrorResult(Error error)
    {
        var body = new ErrorResponse(error.Code, error.Message)
        {
            Errors = error.ValidationErrors
        };

        return error.Type switch
        {
            // 404 Not Found — requested resource does not exist
            ErrorType.NotFound   => new NotFoundObjectResult(body),
            // 401 Unauthorized — the caller could not be authenticated (e.g. wrong credentials, invalid refresh token)
            ErrorType.Unauthorized => new UnauthorizedObjectResult(body),
            // 403 Forbidden — authenticated but not authorized to perform this action
            ErrorType.Forbidden  => new ObjectResult(body) { StatusCode = 403 },
            // 409 Conflict — request conflicts with current state (e.g. duplicate, stale data)
            ErrorType.Conflict   => new ConflictObjectResult(body),
            // 500 Internal Server Error — unhandled or unexpected failure
            ErrorType.Unexpected => new ObjectResult(body) { StatusCode = 500 },
            // 422 Unprocessable Entity — request is well-formed but fails business/domain validation
            ErrorType.Validation => new UnprocessableEntityObjectResult(body),
            // 500 Internal Server Error — a new ErrorType was added without a mapping; the throw reaches
            // GlobalExceptionMiddleware, which returns General.Unexpected
            _ => throw new InvalidOperationException($"Unhandled ErrorType: {error.Type}")
        };
    }
}
