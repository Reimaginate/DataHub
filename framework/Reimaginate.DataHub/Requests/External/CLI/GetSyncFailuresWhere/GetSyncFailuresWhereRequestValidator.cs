using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetSyncFailuresWhere;

public class GetSyncFailuresWhereRequestValidator : AbstractValidator<GetSyncFailuresWhereRequest>
{
    public GetSyncFailuresWhereRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncFailures)).Equal(true).WithMessage("Not Authorized");
    }
}