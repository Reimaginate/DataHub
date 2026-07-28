using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetDataHubEntitiesById;

public class GetDataHubEntitiesByIdRequestValidator : AbstractValidator<GetDataHubEntitiesByIdRequest>
{
    public GetDataHubEntitiesByIdRequestValidator()
    {
        RuleFor(x => x.EntityType).NotNull();
        RuleFor(x => x.EntityIds).NotNull().NotEmpty();
    }
}