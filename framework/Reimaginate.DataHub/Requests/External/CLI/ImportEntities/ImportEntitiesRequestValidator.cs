using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.ImportEntities;

public class ImportEntitiesRequestValidator : AbstractValidator<ImportEntitiesRequest>
{
    public ImportEntitiesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.ImportEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.ImportEntityRequests).NotEmpty();
    }
}