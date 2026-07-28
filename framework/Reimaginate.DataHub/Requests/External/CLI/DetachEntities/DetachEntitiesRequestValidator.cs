using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DetachEntities;

public class DetachEntitiesRequestValidator : AbstractValidator<DetachEntitiesRequest>
{
    public DetachEntitiesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DetachEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(x => x.EntityType).NotEmpty();
        RuleFor(x => x.EntityIds).NotEmpty();
        RuleFor(x => x.DataSource).NotEmpty();
    }
}