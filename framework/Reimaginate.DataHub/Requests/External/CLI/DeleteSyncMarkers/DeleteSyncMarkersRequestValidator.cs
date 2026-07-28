using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteSyncMarkers;

public class DeleteSyncMarkersRequestValidator : AbstractValidator<DeleteSyncMarkersRequest>
{
    public DeleteSyncMarkersRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteSyncMarkers)).Equal(true).WithMessage("Not Authorized");
    }
}