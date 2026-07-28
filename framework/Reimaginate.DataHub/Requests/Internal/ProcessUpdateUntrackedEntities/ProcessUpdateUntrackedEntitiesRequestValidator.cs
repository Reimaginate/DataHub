using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateUntrackedEntities;

public class ProcessUpdateUntrackedEntitiesRequestValidator : AbstractValidator<ProcessUpdateUntrackedEntitiesRequest>
{
    public ProcessUpdateUntrackedEntitiesRequestValidator()
    {
        RuleFor(r => r.Requests).NotEmpty();
        RuleForEach(r => r.Requests).ChildRules(request =>
        {
            request.RuleFor(r => r.EntityType).NotEmpty();
            request.RuleFor(r => r.EntityId).NotEmpty();
            request.RuleFor(r => r.Data).NotNull();
        });
    }
}
