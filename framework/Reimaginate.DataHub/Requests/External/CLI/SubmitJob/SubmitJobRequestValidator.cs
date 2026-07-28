using FluentValidation;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.SubmitJob;

public class SubmitJobRequestValidator : AbstractValidator<SubmitJobRequest>
{
    public SubmitJobRequestValidator()
    {
        RuleFor(r => r.Target).NotEmpty();
        RuleFor(r => r.Target).NotEmpty();
        RuleFor(r => r.Request).NotEmpty();
        RuleFor(r => r.RequestType).NotEmpty();
        RuleFor(r => r.Status)
            .Must(status => status == JobsConstants.Statuses.Draft || status == JobsConstants.Statuses.Ready)
            .WithMessage("Status must be either Draft or Ready.");
    }
}