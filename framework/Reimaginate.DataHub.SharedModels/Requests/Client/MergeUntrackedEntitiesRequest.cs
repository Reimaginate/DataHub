using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class MergeUntrackedEntitiesRequest : DataHubClientRequest<MergeEntitiesResponse>
{
    public MergeUntrackedEntitiesRequest()
    {
        RequestType = nameof(MergeUntrackedEntitiesRequest);
    }
    public string DataSource { get; set; }
    public List<MergeEntityRequest> Requests { get; set; } = new();
}