using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.GetPermissions;

public class GetPermissionsRequestValidator : AbstractValidator<SharedModels.Requests.CLI.GetPermissionsRequest>
{
    public GetPermissionsRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryPermissions)).Equal(true).WithMessage("Not Authorized");
    }
}
