using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetTrackedEntitiesRequest : DataHubClientRequest<GetTrackedEntitiesResponse>
{
    public GetTrackedEntitiesRequest()
    {
        RequestType = nameof(GetTrackedEntitiesRequest);
    }

    public string EntityType { get; set; }
    public string DataSource { get; set; }
    public List<string> EntityIds { get; set; }
}