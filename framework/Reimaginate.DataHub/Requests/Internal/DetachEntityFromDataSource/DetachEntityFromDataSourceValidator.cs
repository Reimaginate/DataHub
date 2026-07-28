using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.DetachEntityFromDataSource;

public class DetachEntityFromDataSourceValidator : AbstractValidator<DetachEntityFromDataSourceRequest>
{
    public DetachEntityFromDataSourceValidator()
    {
        RuleFor(x => x.EntityType).NotEmpty();
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.DataSource).NotEmpty();
    }
}