using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.RevertDataHubEntities;

public class RevertDataHubEntitiesRequestValidator : AbstractValidator<RevertDataHubEntitiesRequest>
{
    public RevertDataHubEntitiesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.PatchEntities)).Equal(true).WithMessage("Not Authorized");

        RuleFor(r => r)
            .Must(HasExactlyOneTargetMode)
            .WithMessage("Specify exactly one revert target mode: EntityType, EntityIds, and RevertTo; or TrackingEntryId.");

        When(r => string.IsNullOrWhiteSpace(r.TrackingEntryId), () =>
        {
            RuleFor(r => r.EntityType).NotEmpty();
            RuleFor(r => r.EntityIds).NotEmpty();
            RuleFor(r => r.RevertTo).NotNull();
        });
    }

    private static bool HasExactlyOneTargetMode(RevertDataHubEntitiesRequest request)
    {
        var hasTrackingEntryId = !string.IsNullOrWhiteSpace(request.TrackingEntryId);
        var hasPointInTimeTarget =
            !string.IsNullOrWhiteSpace(request.EntityType) &&
            request.EntityIds is { Count: > 0 } &&
            request.RevertTo.HasValue;

        return hasTrackingEntryId != hasPointInTimeTarget;
    }
}
