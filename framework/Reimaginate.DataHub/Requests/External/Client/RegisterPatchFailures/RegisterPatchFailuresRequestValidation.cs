using System;
using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterPatchFailures;

public class RegisterPatchFailuresRequestValidation : AbstractValidator<RegisterPatchFailuresRequest>
{
    public RegisterPatchFailuresRequestValidation()
    {
        RuleFor(r => r.PatchFailures).NotEmpty();
        RuleForEach(r => r.PatchFailures).ChildRules(failure =>
        {
            failure.RuleFor(f => f.EventSource).NotEmpty();
            failure.RuleFor(f => f.DataSource).NotEmpty();
            failure.RuleFor(f => f.EntityType).NotEmpty();
            failure.RuleFor(f => f.EntityId).NotEmpty();
            failure.RuleFor(f => f.Patch).NotNull();
            failure.RuleFor(f => f.FailureReason).NotEmpty();
            failure.RuleFor(f => f.Timestamp).NotEqual(default(DateTimeOffset));
        });
    }
}
