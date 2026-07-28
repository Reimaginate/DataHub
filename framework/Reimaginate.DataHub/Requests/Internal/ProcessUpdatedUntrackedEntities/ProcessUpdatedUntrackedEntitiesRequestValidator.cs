using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdatedUntrackedEntities;

public class ProcessUpdatedUntrackedEntitiesRequestValidator : AbstractValidator<ProcessUpdatedUntrackedEntitiesRequest>
{
    public ProcessUpdatedUntrackedEntitiesRequestValidator()
    {
        RuleFor(r => r.DataHubEntityType).NotEmpty();
        RuleFor(r => r.MergeRequests).NotEmpty();
    }
}