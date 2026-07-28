using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.UpdateJobs;

public class UpdateJobsRequestValidator : AbstractValidator<UpdateJobsRequest>
{
    public UpdateJobsRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.SubmitAgentJobs)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.Jobs).NotEmpty();
    }
}
