using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.GetTrackedEntity;

public class GetTrackedEntityRequest : IRequest<JObject>
{
    public string EntityType { get; set; }
    public string DataSource { get; set; }
    public string EntityId { get; set; }
    public DateTimeOffset? AtPointInTime { get; set; }
    public List<ChangeTrackingEntry> TrackingEntries { get; set; }
}