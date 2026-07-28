using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class PatchEntitiesRequest : DataHubClientRequest<PatchEntitiesResponse>
{
    public PatchEntitiesRequest()
    {
        RequestType = nameof(PatchEntitiesRequest);
    }

    public List<PatchEntityRequest> Requests { get; set; }
    public bool DispatchNotifications { get; set; } = false;
    public bool Silent { get; set; }
}