using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DetachEntitiesFromDataSourceRequest : DataHubClientRequest<DetachEntitiesFromDataSourceResponse>
{
    public DetachEntitiesFromDataSourceRequest()
    {
        RequestType = nameof(DetachEntitiesFromDataSourceRequest);
    }

    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; } = new();
}
