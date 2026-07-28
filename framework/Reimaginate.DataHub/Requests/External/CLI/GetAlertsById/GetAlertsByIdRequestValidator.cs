using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetAlertsById;

public class GetAlertsByIdRequestValidator : AbstractValidator<GetAlertsByIdRequest>
{
    public GetAlertsByIdRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryAlerts)).Equal(true).WithMessage("Not Authorized");
        RuleFor(f => f.Ids).NotEmpty();
    }
}