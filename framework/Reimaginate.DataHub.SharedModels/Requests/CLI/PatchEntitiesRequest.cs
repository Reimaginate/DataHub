using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class PatchEntitiesRequest : DataHubCLIRequest<List<PatchEntityResponse>>
{
    public PatchEntitiesRequest()
    {
        RequestType = nameof(PatchEntitiesRequest);
    }

    public List<PatchEntityRequest> Requests { get; set; } = null!;

    public bool DispatchNotifications { get; set; } = false;

    public bool Silent { get; set; }
}