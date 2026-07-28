using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetTrackingData;

public class GetTrackingDataRequestValidator : AbstractValidator<GetTrackingDataRequest>
{
    public GetTrackingDataRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryTrackingData)).Equal(true).WithMessage("Not Authorized");
    }
}