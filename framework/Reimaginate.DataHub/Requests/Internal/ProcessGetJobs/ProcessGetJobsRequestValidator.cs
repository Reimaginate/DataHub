using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ProcessGetJobs;

public class ProcessGetJobsRequestValidator : AbstractValidator<ProcessGetJobsRequest>
{
    public ProcessGetJobsRequestValidator()
    {
        //RuleFor(r => r.).NotEmpty();
    }
}