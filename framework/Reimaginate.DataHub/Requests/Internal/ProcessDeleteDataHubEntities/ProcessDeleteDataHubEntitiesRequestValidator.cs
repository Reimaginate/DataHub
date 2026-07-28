using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteDataHubEntities;

public class ProcessDeleteDataHubEntitiesRequestValidator : AbstractValidator<ProcessDeleteDataHubEntitiesRequest>
{
    public ProcessDeleteDataHubEntitiesRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}