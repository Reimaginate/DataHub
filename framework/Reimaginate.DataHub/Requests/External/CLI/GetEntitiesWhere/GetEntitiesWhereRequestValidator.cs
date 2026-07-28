using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetEntitiesWhere;

public class GetEntitiesWhereRequestValidator : AbstractValidator<GetEntitiesWhereRequest>
{
    public GetEntitiesWhereRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryEntities)).Equal(true).WithMessage("Not Authorized");
    }
}