using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteAlertsRequest : DataHubCLIRequest<DeleteAlertsResponse>
{
    public DeleteAlertsRequest()
    {
        RequestType = nameof(DeleteAlertsRequest);
    }

    public List<string> AlertIds { get; set; }
}