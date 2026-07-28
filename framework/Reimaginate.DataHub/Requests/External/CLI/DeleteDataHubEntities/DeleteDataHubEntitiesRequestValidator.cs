using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteDataHubEntities;

public sealed class DeleteDataHubEntitiesRequestValidator : AbstractValidator<DeleteDataHubEntitiesRequest>
{
    public DeleteDataHubEntitiesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}