using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Agent;

public class SyncEntitiesRequest : AgentRequest<SyncEntitiesResponse>
{
    public SyncEntitiesRequest()
    {
        RequestType = nameof(SyncEntitiesRequest);
    }

    public string DataHubEntityType { get; set; }
    public List<string> DataHubEntityIds { get; set; }
}