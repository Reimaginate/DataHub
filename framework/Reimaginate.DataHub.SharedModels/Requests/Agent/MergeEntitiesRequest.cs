using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Agent;

public class MergeEntitiesRequest : AgentRequest<MergeEntitiesResponse>
{
    public MergeEntitiesRequest()
    {
        RequestType = nameof(MergeEntitiesRequest);
    }

    public string DataHubEntityType { get; set; }
    public List<string> EntityIds { get; set; }
}