using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRegisterAlternateKeys;

public class ProcessRegisterAlternateKeysRequestValidator : AbstractValidator<ProcessRegisterAlternateKeysRequest>
{
    public ProcessRegisterAlternateKeysRequestValidator()
    {
        RuleFor(x => x.Requests).NotEmpty();
    }
}