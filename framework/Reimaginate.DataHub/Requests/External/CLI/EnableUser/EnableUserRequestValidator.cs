using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.EnableUser;

public class EnableUserRequestValidator : AbstractValidator<SharedModels.Requests.CLI.EnableUserRequest>
{
    public EnableUserRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.EnableUsers)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.Id).NotEmpty();
    }
}