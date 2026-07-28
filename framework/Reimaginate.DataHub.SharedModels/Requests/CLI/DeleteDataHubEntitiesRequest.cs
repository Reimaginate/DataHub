using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteDataHubEntitiesRequest : DataHubCLIRequest<DeleteDataHubEntitiesResponse>
{
    public DeleteDataHubEntitiesRequest()
    {
        RequestType = nameof(DeleteDataHubEntitiesRequest);
    }

    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public bool IncludeTrackingEntries { get; set; } = true;
    public bool Silent { get; set; }
}