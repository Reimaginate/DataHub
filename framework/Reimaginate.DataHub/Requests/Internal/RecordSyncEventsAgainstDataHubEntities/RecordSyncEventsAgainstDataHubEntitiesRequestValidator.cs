using FluentValidation;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;



namespace Reimaginate.DataHub.Requests.Internal.RecordSyncEventsAgainstDataHubEntities;

public class RecordSyncEventsAgainstDataHubEntitiesRequestValidator : AbstractValidator<RecordSyncEventsAgainstDataHubEntitiesRequest<SyncEvent>>
{
    public RecordSyncEventsAgainstDataHubEntitiesRequestValidator()
    {
        RuleFor(r => r.SyncEvents).NotEmpty();
    }
}