using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteSyncFailures;

public class DeleteSyncFailuresRequestValidator : AbstractValidator<DeleteSyncFailuresRequest>
{
    public DeleteSyncFailuresRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteLogEntries)).Equal(true).WithMessage("Not Authorized");
    }
}