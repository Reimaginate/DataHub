using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RebaseDataHubEntitiesRequest : DataHubCLIRequest<RebaseDataHubEntitiesResponse>
{
    public RebaseDataHubEntitiesRequest()
    {
        RequestType = nameof(RebaseDataHubEntitiesRequest);
    }
      
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public DateTimeOffset? RebaseTo { get; set; }
}