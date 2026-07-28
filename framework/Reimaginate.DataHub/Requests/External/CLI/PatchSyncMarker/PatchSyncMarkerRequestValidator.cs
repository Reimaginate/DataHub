using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.PatchSyncMarker;

public class PatchSyncMarkerRequestValidator : AbstractValidator<PatchSyncMarkerRequest>
{
    public PatchSyncMarkerRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.UpdateSyncMarkers)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.MarkerId).NotEmpty();
        RuleFor(r => r)
            .Must(r => r.UpdateValue || r.UpdateLastRunTime)
            .WithMessage("Specify at least one sync marker field to patch.");
        RuleFor(r => r.Value)
            .NotNull()
            .When(r => r.UpdateValue)
            .WithMessage("Value is required when UpdateValue is true.");
        RuleFor(r => r.LastRunTime)
            .NotNull()
            .When(r => r.UpdateLastRunTime)
            .WithMessage("LastRunTime is required when UpdateLastRunTime is true.");
    }
}
