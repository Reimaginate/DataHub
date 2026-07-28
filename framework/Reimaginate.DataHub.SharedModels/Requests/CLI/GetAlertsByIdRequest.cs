using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetAlertsByIdRequest : DataHubCLIRequest<GetAlertsResponse>
{
    public GetAlertsByIdRequest()
    {
        RequestType = nameof(GetAlertsByIdRequest);
    }

    public string Select { get; set; }
    public List<string> Ids { get; set; } = new();
}