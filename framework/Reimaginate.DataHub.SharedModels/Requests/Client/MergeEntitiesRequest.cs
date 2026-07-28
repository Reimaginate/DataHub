using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class MergeEntitiesRequest : DataHubClientRequest<MergeEntitiesResponse>
{
    public MergeEntitiesRequest()
    {
        RequestType = nameof(MergeEntitiesRequest);
    }
    public string DataSource { get; set; }
    public string AgentId { get; set; }
    public List<MergeEntityRequest> Requests { get; set; } = new();
}