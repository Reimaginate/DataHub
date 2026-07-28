using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DeleteDataHubEntitiesRequest : DataHubClientRequest<DeleteDataHubEntitiesResponse>
{
    public DeleteDataHubEntitiesRequest()
    {
        RequestType = nameof(DeleteDataHubEntitiesRequest);
    }

    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public bool IncludeTrackingEntries { get; set; } = true;
}