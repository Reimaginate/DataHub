using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntitiesByIdRequest : DataHubClientRequest<GetDataHubEntitiesByIdResponse>
{
    public GetDataHubEntitiesByIdRequest()
    {
        RequestType = nameof(GetDataHubEntitiesByIdRequest);
    }

    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }

}