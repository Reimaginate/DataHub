using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RebaseSourceEntitiesRequest : DataHubCLIRequest<RebaseSourceEntitiesResponse>
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