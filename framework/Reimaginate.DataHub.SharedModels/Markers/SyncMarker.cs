using Reimaginate.DataHub.SharedModels.Core;
using System;

namespace Reimaginate.DataHub.SharedModels.Markers;

public class SyncMarker : CosmosDocument
{
    public SyncMarker()
    {
        _dt = nameof(SyncMarker);
    }
    public string AgentId { get; set; }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string Value { get; set; }
    public DateTimeOffset? LastRunTime { get; set; }
}