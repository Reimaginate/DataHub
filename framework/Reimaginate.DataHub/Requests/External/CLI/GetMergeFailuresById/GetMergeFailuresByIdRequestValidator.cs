using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetMergeFailuresById;

public class GetMergeFailuresByIdRequestValidator : AbstractValidator<GetMergeFailuresByIdRequest>
{
    public GetMergeFailuresByIdRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncFailures)).Equal(true).WithMessage("Not Authorized");
        RuleFor(f => f.Ids).NotEmpty();
    }
}