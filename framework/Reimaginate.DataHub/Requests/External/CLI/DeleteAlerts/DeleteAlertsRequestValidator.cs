using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteAlerts;

public class DeleteAlertsRequestValidator : AbstractValidator<DeleteAlertsRequest>
{
    public DeleteAlertsRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteAlerts)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.AlertIds).NotEmpty();
    }
}
