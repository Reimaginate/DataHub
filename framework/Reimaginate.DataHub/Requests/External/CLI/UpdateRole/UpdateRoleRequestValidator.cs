using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.UpdateRole;

public class UpdateRoleRequestValidator : AbstractValidator<SharedModels.Requests.CLI.UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.UpdateRoles)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.Name).NotEmpty();
        RuleFor(r => r.Permissions).NotEmpty();
    }
}
