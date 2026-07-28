using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;


namespace Reimaginate.DataHub.Requests.External.CLI.RegisterAlternateKeys;

public class RegisterAlternateKeysRequestValidator : AbstractValidator<RegisterAlternateKeysRequest>
{
    public RegisterAlternateKeysRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.RegisterAlternateKeys)).Equal(true).WithMessage("Not Authorized");
        RuleFor(x => x.Requests).NotEmpty();
    }
}