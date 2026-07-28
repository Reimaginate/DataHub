using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetPatchFailuresById;

public class GetPatchFailuresByIdRequestValidator : AbstractValidator<GetPatchFailuresByIdRequest>
{
    public GetPatchFailuresByIdRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncFailures)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.Ids).NotEmpty();
    }
}
