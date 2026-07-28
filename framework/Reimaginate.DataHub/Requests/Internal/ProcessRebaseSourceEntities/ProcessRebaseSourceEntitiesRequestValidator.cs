using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities;

public class ProcessRebaseSourceEntitiesRequestValidator : AbstractValidator<ProcessRebaseSourceEntitiesRequest>
{
    public ProcessRebaseSourceEntitiesRequestValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}