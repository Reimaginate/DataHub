using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.CreateDeferredEntityResolutionPromises;

public class CreateDeferredEntityResolutionPromisesRequestValidator : AbstractValidator<CreateDeferredEntityResolutionPromisesRequest>
{
    public CreateDeferredEntityResolutionPromisesRequestValidator()
    {
        RuleFor(r => r.Entities).NotEmpty();
    }
}