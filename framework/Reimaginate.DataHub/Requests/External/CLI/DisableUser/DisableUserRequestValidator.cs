using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.DisableUser;

public class DisableUserRequestValidator : AbstractValidator<SharedModels.Requests.CLI.DisableUserRequest>
{
    public DisableUserRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DisableUsers)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.Id).NotEmpty();
    }
}