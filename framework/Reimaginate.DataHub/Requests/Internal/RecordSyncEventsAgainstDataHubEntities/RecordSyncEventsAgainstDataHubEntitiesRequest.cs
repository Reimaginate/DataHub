using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;

using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.RecordSyncEventsAgainstDataHubEntities;

public class RecordSyncEventsAgainstDataHubEntitiesRequest<TSyncEvent> : IRequest<NullResponse> where TSyncEvent: SyncEvent
{
    public List<TSyncEvent> SyncEvents { get; set; }
}