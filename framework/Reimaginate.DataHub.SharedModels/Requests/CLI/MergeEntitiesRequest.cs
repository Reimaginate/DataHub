using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class MergeEntitiesRequest : DataHubCLIRequest<MergeEntitiesResponse>
{
    public MergeEntitiesRequest()
    {
        RequestType = nameof(MergeEntitiesRequest);
    }
    
    public string DataSource { get; set; }
    public string DataHubEntityType { get; set; }
    public List<string> SourceEntityIds { get; set; } = new();
}