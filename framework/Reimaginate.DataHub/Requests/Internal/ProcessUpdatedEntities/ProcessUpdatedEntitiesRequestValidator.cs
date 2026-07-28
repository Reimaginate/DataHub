using System.Linq;
using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdatedEntities;

public class ProcessUpdatedEntitiesRequestValidator : AbstractValidator<ProcessUpdatedEntitiesRequest>
{
    public ProcessUpdatedEntitiesRequestValidator()
    {
        RuleFor(r => r.DataHubEntityType).NotEmpty();
        RuleFor(r => r.ConvertedSourceEntityChanges).NotEmpty().When(w => !w.SourceEntityChangesWithAlternateKeys.Any());
        RuleFor(r => r.MergeRequests).NotEmpty();
    }
}