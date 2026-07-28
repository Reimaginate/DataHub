using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteTrackingEntries;

public class DeleteTrackingDataRequestValidator : AbstractValidator<DeleteTrackingEntriesRequest>
{
    public DeleteTrackingDataRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteTrackingData)).Equal(true).WithMessage("Not Authorized");
    }
}