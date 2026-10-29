using FluentValidation;

namespace HrastERP.Administration.Application.Roles.Queries.GetRoles;

internal sealed class GetRolesQueryValidator : AbstractValidator<GetRolesQuery>
{
    public const int MaxPageSize = 100;

    public GetRolesQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);

        // Upper bound keeps (Page - 1) * PageSize from overflowing int in the OFFSET calculation.
        RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue / MaxPageSize);
    }
}
