using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteSyncMarkersRequest : DataHubCLIRequest<DeleteSyncMarkersResponse>
{
    public DeleteSyncMarkersRequest()
    {
        RequestType = nameof(DeleteSyncMarkersRequest);
    }

    public string AgentId { get; set; }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
}