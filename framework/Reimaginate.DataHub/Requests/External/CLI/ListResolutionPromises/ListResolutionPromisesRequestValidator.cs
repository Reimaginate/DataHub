using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.ListResolutionPromises;

public class ListResolutionPromisesRequestValidator : AbstractValidator<ListResolutionPromisesRequest>
{
    public ListResolutionPromisesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.PageSize).GreaterThan(0);
    }
}
