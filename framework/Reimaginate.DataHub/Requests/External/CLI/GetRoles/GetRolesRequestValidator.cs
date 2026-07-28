using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.GetRoles;

public class GetRolesRequestValidator : AbstractValidator<SharedModels.Requests.CLI.GetRolesRequest>
{
    public GetRolesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryRoles)).Equal(true).WithMessage("Not Authorized");
    }
}
