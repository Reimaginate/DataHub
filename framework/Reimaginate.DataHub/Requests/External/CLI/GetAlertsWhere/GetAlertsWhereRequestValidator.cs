using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetAlertsWhere;

public class GetAlertsWhereRequestValidator : AbstractValidator<GetAlertsWhereRequest>
{
    public GetAlertsWhereRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryAlerts)).Equal(true).WithMessage("Not Authorized");
    }
}