using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetLogEntriesWhere;

public class GetLogEntriesWhereRequestValidator : AbstractValidator<GetLogEntriesWhereRequest>
{
    public GetLogEntriesWhereRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncFailures)).Equal(true).WithMessage("Not Authorized");
    }
}
