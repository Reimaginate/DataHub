using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteSourceEntities;

public sealed class DeleteSourceEntitiesRequestValidator : AbstractValidator<DeleteSourceEntitiesRequest>
{
    public DeleteSourceEntitiesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteSourceEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}