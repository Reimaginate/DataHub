using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.DetachEntitiesFromDataSource;

public class DetachEntitiesFromDataSourceRequestValidator : AbstractValidator<DetachEntitiesFromDataSourceRequest>
{
    public DetachEntitiesFromDataSourceRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}