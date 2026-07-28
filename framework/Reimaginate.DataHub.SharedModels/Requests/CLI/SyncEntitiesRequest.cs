using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class SyncEntitiesRequest : DataHubCLIRequest<SyncEntitiesResponse>
{
    public SyncEntitiesRequest()
    {
        RequestType = nameof(SyncEntitiesRequest);
    }
    
    public string DataSource { get; set; }
    public string DataHubEntityType { get; set; }
    public List<string> DataHubEntityIds { get; set; } = new();
}