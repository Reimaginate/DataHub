using Reimaginate.DataHub.SharedModels.Core;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;
public class ImportEntitiesRequest : DataHubCLIRequest<List<ImportEntityResponse>>
{
    public ImportEntitiesRequest()
    {
        RequestType = nameof(ImportEntitiesRequest);
    }

    public List<ImportEntityRequest> ImportEntityRequests { get; set; }

    public bool DispatchNotifications { get; set; } = false;

    public bool Silent { get; set; }

    public bool? Untracked { get; set; }

    public bool? OverwriteIfExists { get; set; }
}