using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteDuplicates;

public class DeleteDuplicatesRequestValidator : AbstractValidator<DeleteDuplicatesRequest>
{
    public DeleteDuplicatesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.BulkDeleteDuplicates)).Equal(true).WithMessage("Not Authorized");
    }
}