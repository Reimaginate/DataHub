using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;

public class GetMaterializedEntitiesByIdRequestValidator : AbstractValidator<GetMaterializedEntitiesByIdRequest>
{
    public GetMaterializedEntitiesByIdRequestValidator()
    {
        RuleFor(x => x.EntityType).NotNull();
        RuleFor(x => x.EntityIds).NotNull().NotEmpty();
    }
}