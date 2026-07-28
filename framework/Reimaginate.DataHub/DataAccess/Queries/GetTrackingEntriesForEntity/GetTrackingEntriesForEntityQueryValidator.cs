using FluentValidation;

namespace Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntriesForEntity;

public class GetTrackingEntriesForEntityQueryValidator : AbstractValidator<GetTrackingEntriesForEntityQuery>
{
    public GetTrackingEntriesForEntityQueryValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityId).NotEmpty();
    }
}