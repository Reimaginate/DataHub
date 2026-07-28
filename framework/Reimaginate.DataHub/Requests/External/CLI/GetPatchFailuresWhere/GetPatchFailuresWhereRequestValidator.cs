using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetPatchFailuresWhere;

public class GetPatchFailuresWhereRequestValidator : AbstractValidator<GetPatchFailuresWhereRequest>
{
    public GetPatchFailuresWhereRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncFailures)).Equal(true).WithMessage("Not Authorized");
    }
}
