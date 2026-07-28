using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.DispatchJobs;

public class DispatchJobsRequestValidator : AbstractValidator<DispatchJobsRequest>
{
    public DispatchJobsRequestValidator()
    {
        RuleFor(r => r.Jobs).NotEmpty();
    }
}