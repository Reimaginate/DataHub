using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.UpdateUser;

public class UpdateUserRequestValidator : AbstractValidator<SharedModels.Requests.CLI.UpdateUserRequest>{
    public UpdateUserRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.UpdateUsers)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.TenantId).NotEmpty();
        RuleFor(r => r.EntraObjectId).NotEmpty();
        RuleFor(r => r.Name).NotEmpty();
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.UPN).NotEmpty();
    }
}
