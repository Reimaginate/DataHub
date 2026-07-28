using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ResolveExternalEntityReferences;

public class ResolveExternalEntityReferencesRequestValidator : AbstractValidator<ResolveExternalEntityReferencesRequest>
{
    public ResolveExternalEntityReferencesRequestValidator()
    {
        RuleFor(r => r.DataHubEntitiesToResolve).NotEmpty();
    }
}
