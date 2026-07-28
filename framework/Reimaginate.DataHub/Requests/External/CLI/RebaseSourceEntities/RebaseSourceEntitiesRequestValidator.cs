using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.RebaseSourceEntities;

public class RebaseSourceEntitiesRequestValidator : AbstractValidator<RebaseSourceEntitiesRequest>
{
    public RebaseSourceEntitiesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.RebaseTrackingData)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}