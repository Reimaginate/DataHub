using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.RetryJob;

public class RetryJobRequestValidator : AbstractValidator<RetryJobRequest>
{
    public RetryJobRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.SubmitAgentJobs)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.JobId).NotNull();
    }
}