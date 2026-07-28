using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;

public class GetTrackedEntityRequestValidator : AbstractValidator<GetTrackedEntityRequest>
{
    public GetTrackedEntityRequestValidator()
    {
        RuleFor(x => x.DataSource).NotEmpty();
        RuleFor(x => x.EntityType).NotEmpty();
        RuleFor(x => x.EntityId).NotEmpty();
    }
}