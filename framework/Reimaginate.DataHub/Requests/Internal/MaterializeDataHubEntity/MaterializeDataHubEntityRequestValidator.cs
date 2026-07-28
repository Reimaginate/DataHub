using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;

public class MaterializeDataHubEntityRequestValidator : AbstractValidator<MaterializeDataHubEntityRequest>
{
    public MaterializeDataHubEntityRequestValidator()
    {
        RuleFor(x => x.EntityType).NotNull();
        RuleFor(x => x.EntityId).NotNull();
    }
}