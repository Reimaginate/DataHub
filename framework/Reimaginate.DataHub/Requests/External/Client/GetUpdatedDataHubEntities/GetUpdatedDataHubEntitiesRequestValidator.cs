using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetUpdatedDataHubEntities;

public class GetUpdatedDataHubEntitiesRequestValidator : AbstractValidator<GetUpdatedDataHubEntitiesRequest>
{
    public GetUpdatedDataHubEntitiesRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.FromDateTime).NotEmpty();
    }
}