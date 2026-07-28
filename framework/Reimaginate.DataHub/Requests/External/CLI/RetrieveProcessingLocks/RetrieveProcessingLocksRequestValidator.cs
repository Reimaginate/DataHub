using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.RetrieveProcessingLocks;

public class RetrieveProcessingLocksRequestValidator : AbstractValidator<RetrieveProcessingLocksRequest>
{
    public RetrieveProcessingLocksRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryProcessingLocks)).Equal(true).WithMessage("Not Authorized");
    }
}