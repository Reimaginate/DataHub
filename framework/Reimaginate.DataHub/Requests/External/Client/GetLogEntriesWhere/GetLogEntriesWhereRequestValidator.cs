using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.Client.GetLogEntriesWhere;

public class GetLogEntriesWhereRequestValidator : AbstractValidator<GetSyncFailuresWhereRequest>
{
    public GetLogEntriesWhereRequestValidator()
    {
        //RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncFailures)).Equal(true).WithMessage("Not Authorized");
    }
}