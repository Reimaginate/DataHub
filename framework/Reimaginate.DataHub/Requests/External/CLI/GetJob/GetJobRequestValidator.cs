using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetJob;

public class GetJobRequestValidator : AbstractValidator<GetJobRequest>
{
    public GetJobRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryJobs)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.JobId).NotNull();
    }
}