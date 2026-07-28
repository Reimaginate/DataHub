using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRevertDataHubEntities;

public class ProcessRevertDataHubEntitiesRequest : IRequest<ProcessRevertDataHubEntitiesResponse>
{
    public string CorrelationId { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; } = new();
    public DateTimeOffset? RevertTo { get; set; }
    public string TrackingEntryId { get; set; }
    public bool DispatchNotifications { get; set; } = true;
    public bool DryRun { get; set; }
}
