using System;
using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterSyncFailures;

public class RegisterSyncFailuresRequestValidation : AbstractValidator<RegisterSyncFailuresRequest>
{
    public RegisterSyncFailuresRequestValidation()
    {
        RuleFor(r => r.SyncFailures).NotEmpty();
        RuleForEach(r => r.SyncFailures).ChildRules(failure =>
        {
            failure.RuleFor(f => f.DataSource).NotEmpty();
            failure.RuleFor(f => f.DataHubEntityType).NotEmpty();
            failure.RuleFor(f => f.DataHubEntityId).NotEmpty();
            failure.RuleFor(f => f.FailureType).NotEmpty();
            failure.RuleFor(f => f.FailureReason).NotEmpty();
            failure.RuleFor(f => f.Timestamp).NotEqual(default(DateTimeOffset));
        });
    }
}
