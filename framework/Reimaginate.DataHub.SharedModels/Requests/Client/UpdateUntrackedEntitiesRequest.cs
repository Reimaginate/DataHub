using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateUntrackedEntitiesRequest : DataHubClientRequest<UpdateUntrackedEntitiesResponse>
{
    public UpdateUntrackedEntitiesRequest()
    {
        RequestType = nameof(UpdateUntrackedEntitiesRequest);
    }

    public List<UpdateUntrackedEntityRequest> Requests { get; set; }

    public bool DispatchNotifications { get; set; }

    public bool Silent { get; set; }
}