using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetEntitiesById;

public class GetEntitiesByIdRequestValidator : AbstractValidator<GetEntitiesByIdRequest>
{
    public GetEntitiesByIdRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityIds).NotEmpty();
    }
}