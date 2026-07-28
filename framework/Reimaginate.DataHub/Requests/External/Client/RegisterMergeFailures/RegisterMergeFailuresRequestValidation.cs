using System;
using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterMergeFailures;

public class RegisterMergeFailuresRequestValidation : AbstractValidator<RegisterMergeFailuresRequest>
{
    public RegisterMergeFailuresRequestValidation()
    {
        RuleFor(r => r.MergeFailures).NotEmpty();
        RuleForEach(r => r.MergeFailures).ChildRules(failure =>
        {
            failure.RuleFor(f => f.DataSource).NotEmpty();
            failure.RuleFor(f => f.SourceEntityType).NotEmpty();
            failure.RuleFor(f => f.SourceEntityId).NotEmpty();
            failure.RuleFor(f => f.FailureType).NotEmpty();
            failure.RuleFor(f => f.FailureReason).NotEmpty();
            failure.RuleFor(f => f.Timestamp).NotEqual(default(DateTimeOffset));
        });
    }
}
