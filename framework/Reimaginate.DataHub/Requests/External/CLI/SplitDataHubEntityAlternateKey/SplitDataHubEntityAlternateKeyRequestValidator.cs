using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.SplitDataHubEntityAlternateKey;

public class SplitDataHubEntityAlternateKeyRequestValidator : AbstractValidator<SplitDataHubEntityAlternateKeyRequest>
{
    public SplitDataHubEntityAlternateKeyRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.PatchEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.EntityId).NotEmpty();
        RuleFor(r => r.Key).NotEmpty();
        RuleFor(r => r.Value).NotEmpty();
    }
}
