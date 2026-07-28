using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.RegisterJobs;

public class RegisterJobsRequestValidator : AbstractValidator<RegisterJobsRequest>
{
    public RegisterJobsRequestValidator()
    {
        RuleFor(r => r.JobRequests).NotEmpty();

        RuleForEach(r => r.JobRequests).ChildRules(r =>
        {
            r.RuleFor(p => p.Target).NotEmpty();
            r.RuleFor(p => p.JobType).NotEmpty();
            r.RuleFor(p => p.Request).NotNull();
        });
    }
}