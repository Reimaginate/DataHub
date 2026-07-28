using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.UpdateSyncMarker;

public class UpdateSyncMarkerRequestValidator : AbstractValidator<UpdateSyncMarkerRequest>
{
    public UpdateSyncMarkerRequestValidator()
    {
        RuleFor(r => r.SyncMarker).NotNull();
        RuleFor(r => r.SyncMarker.id).NotEmpty();
        RuleFor(r => r.SyncMarker.AgentId).NotEmpty();
        RuleFor(r => r.SyncMarker.DataSource).NotEmpty();
        RuleFor(r => r.SyncMarker.EntityType).NotEmpty();
        RuleFor(r => r.NewValue).NotEmpty();
    }
}
