using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.MergeUntrackedEntities;

public class MergeUntrackedEntitiesRequestValidator : AbstractValidator<MergeUntrackedEntitiesRequest>
{
    public MergeUntrackedEntitiesRequestValidator()
    {
        RuleFor(x => x.RequestType).NotEmpty();
        RuleFor(x => x.Requests).NotEmpty();
        RuleForEach(x => x.Requests).ChildRules(requests =>
        {
            requests.RuleFor(x => x.DataSource).NotEmpty();
            //requests.RuleFor(x => x.AgentId).NotEmpty();
            requests.RuleFor(x => x.SourceEntityType).NotEmpty();
            requests.RuleFor(x => x.SourceEntityId).NotEmpty();
            requests.RuleFor(x => x.DataHubEntityType).NotEmpty();
            requests.RuleFor(x => x.Data).NotEmpty();
        });
    }
}