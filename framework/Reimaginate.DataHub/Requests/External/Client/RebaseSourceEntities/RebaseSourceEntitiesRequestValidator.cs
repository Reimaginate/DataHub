using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.RebaseSourceEntities;

public class RebaseSourceEntitiesRequestValidator : AbstractValidator<RebaseSourceEntitiesRequest>
{
    public RebaseSourceEntitiesRequestValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}