using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.ResolveEntityReferences;

public class ResolveEntityReferencesRequestValidator : AbstractValidator<ResolveEntityReferencesRequest>
{
    public ResolveEntityReferencesRequestValidator()
    {
        RuleFor(x => x.EntityReferences).NotEmpty();
    }
}