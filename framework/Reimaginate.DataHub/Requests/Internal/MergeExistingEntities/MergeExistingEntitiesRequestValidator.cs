using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.MergeExistingEntities;

public class MergeExistingEntitiesRequestValidator : AbstractValidator<MergeExistingEntitiesRequest>
{
    public MergeExistingEntitiesRequestValidator()
    {

        RuleFor(r => r.MergeRequests).NotEmpty();
    }
}