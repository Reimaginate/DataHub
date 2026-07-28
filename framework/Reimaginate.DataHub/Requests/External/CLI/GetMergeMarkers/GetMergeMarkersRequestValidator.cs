using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetMergeMarkers;

public class GetMergeMarkersRequestValidator : AbstractValidator<GetMergeMarkersRequest>
{
    public GetMergeMarkersRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncMarkers)).Equal(true).WithMessage("Not Authorized");
    }
}