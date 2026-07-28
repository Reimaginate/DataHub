using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.RegisterUser;

public class RegisterUserRequestValidator : AbstractValidator<SharedModels.Requests.CLI.RegisterUserRequest>{
    public RegisterUserRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.RegisterUsers)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.TenantId).NotEmpty();
        RuleFor(r => r.EntraObjectId).NotEmpty();
        RuleFor(r => r.Name).NotEmpty();
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.UPN).NotEmpty();
    }
}
