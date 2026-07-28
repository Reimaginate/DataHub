using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetEntitiesByIdRequest : DataHubCLIRequest<GetEntitiesResponse>
{
    public GetEntitiesByIdRequest()
    {
        RequestType = nameof(GetEntitiesByIdRequest);
    }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; } = new();
}