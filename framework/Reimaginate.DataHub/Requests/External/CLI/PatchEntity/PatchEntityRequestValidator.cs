using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.PatchEntity;

public class PatchEntityRequestValidator : AbstractValidator<PatchEntityRequest>
{
    public PatchEntityRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.PatchEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.DataSource).NotEmpty();
        RuleFor(r => r.EntityId).NotEmpty();
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.Operations).NotEmpty();
    }

}