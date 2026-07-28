using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKey;

public class ProcessRegisterAlternateKeyRequestValidator : AbstractValidator<ProcessRegisterAlternateKeyRequest>
{
    public ProcessRegisterAlternateKeyRequestValidator()
    {
        RuleFor(x => x.EntityType).NotEmpty();
        RuleFor(x => x.Key).NotEmpty();
        RuleFor(x => x.SourceEntityId).NotEmpty();
        RuleFor(x => x.DataHubEntityId).NotEmpty();
    }
}