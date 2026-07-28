using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.UpdateTrackedEntity;

public class UpdateTrackedEntityRequestValidator : AbstractValidator<UpdateTrackedEntityRequest>
{
    public UpdateTrackedEntityRequestValidator()
    {
        RuleFor(x => x.DataSource).NotNull();
        RuleFor(x => x.SourceEntityId).NotNull();
        RuleFor(x => x.EntityType).NotNull();
        RuleFor(x => x.EntityData).NotNull();
    }
}