using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateEntities;

public class ProcessUpdateEntitiesRequestValidator : AbstractValidator<ProcessUpdateEntitiesRequest>
{
    public ProcessUpdateEntitiesRequestValidator()
    {
        RuleFor(r => r.Requests).NotEmpty();
    }
}