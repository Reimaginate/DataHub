using FluentValidation;
using Reimaginate.DataHub.Auth;

namespace Reimaginate.DataHub.Requests.External.CLI.GetUsers;

public class GetUsersRequestValidator : AbstractValidator<SharedModels.Requests.CLI.GetUsersRequest>{
    public GetUsersRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryUsers)).Equal(true).WithMessage("Not Authorized");
    }
}