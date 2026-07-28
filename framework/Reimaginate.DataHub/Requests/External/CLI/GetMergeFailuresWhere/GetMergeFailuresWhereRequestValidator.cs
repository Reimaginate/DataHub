using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetMergeFailuresWhere;

public class GetMergeFailuresWhereRequestValidator : AbstractValidator<GetMergeFailuresWhereRequest>
{
    public GetMergeFailuresWhereRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncFailures)).Equal(true).WithMessage("Not Authorized");
    }
}