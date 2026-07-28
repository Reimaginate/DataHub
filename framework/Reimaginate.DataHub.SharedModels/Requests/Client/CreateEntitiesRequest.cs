using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class CreateEntitiesRequest : DataHubClientRequest<CreateEntitiesResponse>
{
    public CreateEntitiesRequest()
    {
        RequestType = nameof(CreateEntitiesRequest);
    }

    public List<CreateEntityRequest> Requests { get; set; }
}