using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteMergeFailures;

public class DeleteMergeFailuresRequestValidator : AbstractValidator<DeleteMergeFailuresRequest>
{
    public DeleteMergeFailuresRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteLogEntries)).Equal(true).WithMessage("Not Authorized");
    }
}