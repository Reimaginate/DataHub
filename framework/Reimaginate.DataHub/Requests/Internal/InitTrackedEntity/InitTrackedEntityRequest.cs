using System;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;

public class InitTrackedEntityRequest : IRequest<ChangeTrackingEntry>
{
    public string DataSource { get; set; }
    public string EntityId { get; set; }
    public string EntityType { get; set; }
    public JObject EntityData { get; set; }
    public DateTimeOffset? Timestamp { get; set; } 
}