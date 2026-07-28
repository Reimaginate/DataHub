using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.ImportEntity;

public class ImportEntityRequestValidator : AbstractValidator<ImportEntityRequest>
{
    public ImportEntityRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.ImportEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.EntityId).NotEmpty();
        RuleFor(r => r.EntityType).NotEmpty();
        RuleFor(r => r.Data).NotEmpty();
    }

}