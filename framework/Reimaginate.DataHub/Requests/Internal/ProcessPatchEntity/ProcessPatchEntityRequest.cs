using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;

public class ProcessPatchEntityRequest : IRequest<ProcessPatchEntityResponse>
{
    public string RequestId { get; set; }
    public string DataSource { get; set; }

    public string EntityType { get; set; }

    public string EntityId { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public List<Patch> Operations { get; set; } = new();

    public string CorrelationId { get; set; }

    public Dictionary<string, object> Cache { get; set; } = new();

    public bool CommitToDb { get; set; } = true;

    public bool DispatchNotifications { get; set; }

    public bool Silent { get; set; }

    public bool DoNotTrack { get; set; }
}
