using System;
using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class PatchEntityRequest : DataHubCLIRequest<PatchEntityResponse>
{
    public PatchEntityRequest()
    {
        RequestType = nameof(PatchEntityRequest);
    }
    public string DataSource { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public List<Patch> Operations { get; set; } = new();
    public bool DispatchNotifications { get; set; } = false;
    public bool Silent { get; set; }
}