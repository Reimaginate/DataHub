using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceResolutionPromises;

public class ResolveEntityReferenceResolutionPromisesRequestValidator: AbstractValidator<ResolveEntityReferenceResolutionPromisesRequest>
{
    public ResolveEntityReferenceResolutionPromisesRequestValidator()
    {
        RuleFor(r => r.SourceSystemEntityIds).NotEmpty();
        RuleFor(r => r.ResolvedReferencedEntities).NotEmpty();
    }
}