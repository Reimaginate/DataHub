using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetAlternateKeysWhere;

public class GetDataHubEntityTypeCountsRequestValidator : AbstractValidator<GetAlternateKeysWhereRequest>
{
    public GetDataHubEntityTypeCountsRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteSourceEntities)).Equal(true).WithMessage("Not Authorized");
    }
}