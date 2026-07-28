using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetSyncMarkers;

public class GetSyncMarkersRequestValidator : AbstractValidator<GetSyncMarkersRequest>
{
    public GetSyncMarkersRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncMarkers)).Equal(true).WithMessage("Not Authorized");
    }
}