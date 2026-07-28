using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RevertDataHubEntitiesRequest : DataHubCLIRequest<RevertDataHubEntitiesResponse>
{
    public RevertDataHubEntitiesRequest()
    {
        RequestType = nameof(RevertDataHubEntitiesRequest);
    }

    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; } = new();
    public DateTimeOffset? RevertTo { get; set; }
    public string TrackingEntryId { get; set; }
    public bool DispatchNotifications { get; set; } = true;
    public bool DryRun { get; set; }
}
