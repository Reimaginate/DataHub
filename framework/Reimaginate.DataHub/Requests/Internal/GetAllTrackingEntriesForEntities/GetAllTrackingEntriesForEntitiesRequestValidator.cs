using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.GetAllTrackingEntriesForEntities;

public class GetAllTrackingEntriesForEntitiesRequestValidator : AbstractValidator<GetAllTrackingEntriesForEntitiesRequest>
{
    public GetAllTrackingEntriesForEntitiesRequestValidator()
    {
        RuleFor(x => x.DataSource).NotEmpty();
        RuleFor(x => x.EntityType).NotEmpty();
        RuleFor(x => x.EntityIds).NotEmpty();
    }
}