using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessDeleteDataHubEntities;

public class ProcessDeleteDataHubEntitiesRequest : IRequest<ProcessDeleteDataHubEntitiesResponse>
{
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public List<JObject> Entities { get; set; }
    public bool IncludeTrackingEntries { get; set; } = true;
}