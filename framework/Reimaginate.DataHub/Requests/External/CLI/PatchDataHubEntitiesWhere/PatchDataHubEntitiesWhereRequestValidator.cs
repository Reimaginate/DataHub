using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.PatchDataHubEntitiesWhere;

public class PatchDataHubEntitiesWhereRequestValidator : AbstractValidator<PatchDataHubEntitiesWhereRequest>
{
    public PatchDataHubEntitiesWhereRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.PatchEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.Where).NotEmpty();
        RuleFor(r => r.Operations).NotEmpty();
    }
}
