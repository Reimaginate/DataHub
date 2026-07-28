using System;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class RebaseSourceEntitiesRequest : DataHubClientRequest<RebaseSourceEntitiesResponse>
{
    public RebaseSourceEntitiesRequest()
    {
        RequestType = nameof(RebaseSourceEntitiesRequest);
    }
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public DateTimeOffset? RebaseTo { get; set; }
}