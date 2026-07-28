using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetJobs;

public class GetJobsRequestValidator : AbstractValidator<GetJobsRequest>
{
    public GetJobsRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryJobs)).Equal(true).WithMessage("Not Authorized");
    }
}