using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.RegisterAlternateKey;

public class RegisterAlternateKeyRequestValidator : AbstractValidator<RegisterAlternateKeyRequest>
{
    public RegisterAlternateKeyRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.RegisterAlternateKeys)).Equal(true).WithMessage("Not Authorized");
        RuleFor(x => x.EntityType).NotEmpty();
        RuleFor(x => x.Key).NotEmpty();
        RuleFor(x => x.SourceEntityId).NotEmpty();
        RuleFor(x => x.DataHubEntityId).NotEmpty();
    }
}