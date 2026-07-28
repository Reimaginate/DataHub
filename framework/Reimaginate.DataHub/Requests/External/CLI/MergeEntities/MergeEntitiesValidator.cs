using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.MergeEntities;

public class MergeEntitiesValidator : AbstractValidator<MergeEntitiesRequest>
{
    public MergeEntitiesValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.SubmitAgentJobs)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.DataHubEntityType).NotEmpty();
        RuleFor(r => r.SourceEntityIds).NotEmpty();
    }
}