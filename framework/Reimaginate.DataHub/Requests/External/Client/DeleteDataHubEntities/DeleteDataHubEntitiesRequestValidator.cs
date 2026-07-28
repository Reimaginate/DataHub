using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.DeleteDataHubEntities;

public sealed class DeleteDataHubEntitiesRequestValidator : AbstractValidator<DeleteDataHubEntitiesRequest>
{
    public DeleteDataHubEntitiesRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}