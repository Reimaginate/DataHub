using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;
namespace Reimaginate.DataHub.Requests.External.Client.RegisterAlternateKey;

public class RegisterAlternateKeyRequestValidator : AbstractValidator<RegisterAlternateKeyRequest>
{
    public RegisterAlternateKeyRequestValidator()
    {
        RuleFor(x => x.EntityType).NotEmpty();
        RuleFor(x => x.Key).NotEmpty();
        RuleFor(x => x.SourceEntityId).NotEmpty();
        RuleFor(x => x.DataHubEntityId).NotEmpty();
    }
}