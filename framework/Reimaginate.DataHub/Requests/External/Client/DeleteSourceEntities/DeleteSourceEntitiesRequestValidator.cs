using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;


namespace Reimaginate.DataHub.Requests.External.Client.DeleteSourceEntities;

public sealed class DeleteSourceEntitiesRequestValidator : AbstractValidator<DeleteSourceEntitiesRequest>
{
    public DeleteSourceEntitiesRequestValidator()
    {
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}