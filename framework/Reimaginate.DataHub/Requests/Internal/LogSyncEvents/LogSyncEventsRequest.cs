using System.Collections.Generic;
using Reimaginate.DataHub.Requests.Internal.LogEvents;
using Reimaginate.DataHub.SharedModels.Core.Models.Events;

using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.LogSyncEvents;

public class LogSyncEventsRequest<TSyncEvent> : IRequest<LogEventsResponse> where TSyncEvent : SyncEvent
{
    public List<TSyncEvent> SyncEvents { get; set; }
}