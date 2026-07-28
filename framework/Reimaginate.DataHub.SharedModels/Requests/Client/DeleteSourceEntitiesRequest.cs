using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DeleteSourceEntitiesRequest : DataHubClientRequest<DeleteSourceEntitiesResponse>
{
    public DeleteSourceEntitiesRequest()
    {
        RequestType = nameof(DeleteSourceEntitiesRequest);
    }

    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
}