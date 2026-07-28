using System.Collections.Generic;

namespace Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;

public class GetMaterializedEntitiesByIdRequest : DataHubClientRequest<GetMaterializedEntitiesByIdResponse>
{
    public GetMaterializedEntitiesByIdRequest()
    {
        RequestType = nameof(GetMaterializedEntitiesByIdRequest);
    }

    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; } = new();

}
