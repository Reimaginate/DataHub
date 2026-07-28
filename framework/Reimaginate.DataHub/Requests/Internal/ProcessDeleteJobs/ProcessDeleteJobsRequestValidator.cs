using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteJobs;

public class ProcessDeleteJobsRequestValidator : AbstractValidator<ProcessDeleteJobsRequest>
{
    public ProcessDeleteJobsRequestValidator()
    {
        RuleFor(r => r.JobIds).NotEmpty();
    }
}