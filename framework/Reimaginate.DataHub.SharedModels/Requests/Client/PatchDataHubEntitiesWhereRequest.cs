using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class PatchDataHubEntitiesWhereRequest : DataHubClientRequest<PatchDataHubEntitiesWhereResponse>
{
    public PatchDataHubEntitiesWhereRequest()
    {
        RequestType = nameof(PatchDataHubEntitiesWhereRequest);
    }

    public string EntityType { get; set; }
    public List<Patch> Operations { get; set; } = new();
    public DateTimeOffset? Timestamp { get; set; }
    public string Where { get; set; }
    public string Select { get; set; }
    public string From { get; set; }
    public string OrderBy { get; set; }
    public int PageSize { get; set; } = 100;
    public string ContinuationToken { get; set; }
    public bool GetTotalResultCount { get; set; }
    public bool DispatchNotifications { get; set; } = false;
    public bool Silent { get; set; }
}