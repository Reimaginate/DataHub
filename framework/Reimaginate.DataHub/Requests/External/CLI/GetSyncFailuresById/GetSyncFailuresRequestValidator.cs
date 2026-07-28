using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetSyncFailuresById;

public class GetSyncFailuresByIdRequestValidator : AbstractValidator<GetSyncFailuresByIdRequest>
{
    public GetSyncFailuresByIdRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QuerySyncFailures)).Equal(true).WithMessage("Not Authorized");
        RuleFor(f => f.Ids).NotEmpty();
    }
}