using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetDuplicate;

public class GetDuplicateRequestValidator : AbstractValidator<GetDuplicateRequest>
{
    public GetDuplicateRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.ReadDuplicates)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.Id).NotNull();
    }
}