using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.SyncEntities;

public class SyncEntitiesRequestValidator : AbstractValidator<SyncEntitiesRequest>
{
    public SyncEntitiesRequestValidator()
    {
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.DataHubEntityType).NotEmpty();
        RuleFor(r => r.DataHubEntityIds).NotEmpty();
    }
}