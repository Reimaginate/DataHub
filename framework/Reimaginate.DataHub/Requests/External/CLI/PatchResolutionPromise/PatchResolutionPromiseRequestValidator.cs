using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.PatchResolutionPromise;

public class PatchResolutionPromiseRequestValidator : AbstractValidator<PatchResolutionPromiseRequest>
{
    public PatchResolutionPromiseRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.PatchEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.PromiseId).NotEmpty();
        RuleFor(r => r.Operations).NotEmpty();
    }
}
