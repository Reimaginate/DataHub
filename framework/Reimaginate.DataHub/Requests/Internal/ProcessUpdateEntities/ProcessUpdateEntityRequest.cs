using System;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateEntities;

public class ProcessUpdateEntityRequest 
{
    public string UpdateType { get; set; }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public JObject Data { get; set; }
    public bool CreateOnly { get; set; }
    public bool UpdateOnly { get; set; }
    public bool ReturnResultingEntity { get; set; } = false;
}