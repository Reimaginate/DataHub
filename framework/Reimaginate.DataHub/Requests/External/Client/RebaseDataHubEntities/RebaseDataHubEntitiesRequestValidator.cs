using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.RebaseDataHubEntities;

public class RebaseDataHubEntitiesRequestValidator : AbstractValidator<RebaseDataHubEntitiesRequest>
{
    public RebaseDataHubEntitiesRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}