using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.DeleteLogEntries;

public class DeleteLogEntriesRequestValidator : AbstractValidator<DeleteLogEntriesRequest>
{
    public DeleteLogEntriesRequestValidator()
    {
        //RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteLogEntries)).Equal(true).WithMessage("Not Authorized");
    }
}