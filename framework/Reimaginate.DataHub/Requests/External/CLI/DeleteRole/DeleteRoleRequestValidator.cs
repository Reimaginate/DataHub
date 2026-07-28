using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteRole;

public class DeleteRoleRequestValidator : AbstractValidator<SharedModels.Requests.CLI.DeleteRoleRequest>
{
    public DeleteRoleRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteRoles)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.Name).NotEmpty();
    }
}
