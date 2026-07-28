using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateEntitiesRequest : DataHubClientRequest<UpdateEntitiesResponse>
{
    public UpdateEntitiesRequest()
    {
        RequestType = nameof(UpdateEntitiesRequest);
    }

    public List<UpdateEntityRequest> Requests { get; set; }
}