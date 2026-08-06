using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetResolutionPromises;

public class GetResolutionPromisesRequestValidator : AbstractValidator<GetResolutionPromisesRequest>
{
    public GetResolutionPromisesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.PromiseIds).NotEmpty();
    }
}
