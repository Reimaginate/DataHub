using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateJobs;

public class ProcessUpdateJobsRequestValidator : AbstractValidator<ProcessUpdateJobsRequest>
{
    public ProcessUpdateJobsRequestValidator()
    {
        RuleFor(r => r.Jobs).NotEmpty();
    }
}