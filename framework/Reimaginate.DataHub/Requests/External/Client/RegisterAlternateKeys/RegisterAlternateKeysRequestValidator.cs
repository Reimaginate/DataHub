using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterAlternateKeys;

public class RegisterAlternateKeysRequestValidator : AbstractValidator<RegisterAlternateKeysRequest>
{
    public RegisterAlternateKeysRequestValidator()
    {
        RuleFor(x => x.Requests).NotEmpty();
    }
}