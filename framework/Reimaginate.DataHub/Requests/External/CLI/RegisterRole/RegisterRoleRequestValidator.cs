using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.RegisterRole;

public class RegisterRoleRequestValidator : AbstractValidator<SharedModels.Requests.CLI.RegisterRoleRequest>
{
    public RegisterRoleRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.RegisterRoles)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.Name).NotEmpty();
        RuleFor(r => r.Permissions).NotEmpty();
    }
}
