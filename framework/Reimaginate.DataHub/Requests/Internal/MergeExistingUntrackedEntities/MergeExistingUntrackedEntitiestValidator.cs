using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.MergeExistingUntrackedEntities;

public class MergeExistingUntrackedEntitiestValidator : AbstractValidator<MergeExistingUntrackedEntitiesRequest>
{
    public MergeExistingUntrackedEntitiestValidator()
    {

        RuleFor(r => r.MergeRequests).NotEmpty();
    }
}