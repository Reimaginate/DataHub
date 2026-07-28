using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DetachEntitiesRequest : DataHubCLIRequest<DetachEntitiesResponse>
{
    public DetachEntitiesRequest()
    {
        RequestType = nameof(DetachEntitiesRequest);
    }
       
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; } = new();
    public string DataSource { get; set; }
}