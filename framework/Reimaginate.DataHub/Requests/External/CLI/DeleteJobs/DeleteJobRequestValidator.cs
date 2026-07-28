using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteJobs;

public class DeleteJobsRequestValidator : AbstractValidator<DeleteJobsRequest>
{
    public DeleteJobsRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.BulkDeleteJobs)).Equal(true).WithMessage("Not Authorized");
    }
}