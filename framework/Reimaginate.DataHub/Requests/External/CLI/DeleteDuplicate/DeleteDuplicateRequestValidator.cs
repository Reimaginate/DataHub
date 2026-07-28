using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteDuplicate;

public class DeleteDuplicateRequestValidator : AbstractValidator<DeleteDuplicateRequest>
{
    public DeleteDuplicateRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteDuplicates)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.Id).NotEmpty();
    }
}