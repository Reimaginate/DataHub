using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteJob;

public class DeleteJobRequestValidator : AbstractValidator<DeleteJobRequest>
{
    public DeleteJobRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.DeleteJobs)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.JobId).NotEmpty();
    }
}