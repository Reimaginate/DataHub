using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;

public class ProcessRebaseDataHubEntitiesRequestValidator : AbstractValidator<ProcessRebaseDataHubEntitiesRequest>
{
    public ProcessRebaseDataHubEntitiesRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}