using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.DetachEntitiesFromDataSource;

public sealed class DetachEntitiesFromDataSourceRequestValidator : AbstractValidator<DetachEntitiesFromDataSourceRequest>
{
    public DetachEntitiesFromDataSourceRequestValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}
