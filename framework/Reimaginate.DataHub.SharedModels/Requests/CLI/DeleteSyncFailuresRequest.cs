using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteSyncFailuresRequest : DataHubCLIRequest<DeleteSyncFailuresResponse>
{
    public DeleteSyncFailuresRequest()
    {
        RequestType = nameof(DeleteSyncFailuresRequest);
    }

    public List<string> SyncFailureIds { get; set; }
}