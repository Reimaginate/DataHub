using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeletePatchFailures;

public class DeletePatchFailuresRequestValidator : AbstractValidator<DeletePatchFailuresRequest>
{
    public DeletePatchFailuresRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteLogEntries)).Equal(true).WithMessage("Not Authorized");
    }
}