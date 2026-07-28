using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetTrackedEntities;

public class GetTrackedEntitiesRequestValidator : AbstractValidator<GetTrackedEntitiesRequest>
{
    public GetTrackedEntitiesRequestValidator()
    {
        RuleFor(x => x.DataSource).NotEmpty();
        RuleFor(x => x.EntityIds).NotEmpty();
        RuleFor(x => x.EntityType).NotEmpty();
    }
}