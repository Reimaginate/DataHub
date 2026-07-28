using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.MergeNewEntities;

public class MergeNewEntitiesRequestValidator : AbstractValidator<MergeNewEntitiesRequest>
{
    public MergeNewEntitiesRequestValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.DataHubEntityType).NotEmpty();
        RuleFor(r => r.MergeRequests).NotEmpty();
    }
}