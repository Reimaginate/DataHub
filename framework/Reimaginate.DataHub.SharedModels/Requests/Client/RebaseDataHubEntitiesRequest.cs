using System;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RebaseDataHubEntitiesRequest : DataHubClientRequest<RebaseDataHubEntitiesResponse>
{
    public RebaseDataHubEntitiesRequest()
    {
        RequestType = nameof(RebaseDataHubEntitiesRequest);
    }
      
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public DateTimeOffset? RebaseTo { get; set; }
}