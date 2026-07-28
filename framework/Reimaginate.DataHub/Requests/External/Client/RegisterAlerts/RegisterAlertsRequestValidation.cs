using System;
using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.RegisterAlerts;

public class RegisterAlertsRequestValidation : AbstractValidator<RegisterAlertsRequest>
{
    public RegisterAlertsRequestValidation()
    {
        RuleFor(r => r.Alerts).NotEmpty();
        RuleForEach(r => r.Alerts).ChildRules(alert =>
        {
            alert.RuleFor(a => a.Severity).NotEmpty();
            alert.RuleFor(a => a.Subject).NotEmpty();
            alert.RuleFor(a => a.Description).NotEmpty();
            alert.RuleFor(a => a.Timestamp).NotEqual(default(DateTimeOffset));
        });
    }
}
