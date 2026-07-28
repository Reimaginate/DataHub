using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetEntitiesByAltKey;

public class GetEntitiesByAltKeyRequestValidator : AbstractValidator<GetEntitiesByAltKeyRequest>
{
    public GetEntitiesByAltKeyRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.AlternateKeys).NotEmpty();
    }
}
