using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetDuplicates;

public class GetDuplicatesRequestValidator : AbstractValidator<GetDuplicatesRequest>
{
    public GetDuplicatesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.ReadDuplicates)).Equal(true).WithMessage("Not Authorized");
    }
}