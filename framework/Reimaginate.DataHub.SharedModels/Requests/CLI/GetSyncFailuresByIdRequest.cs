using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetSyncFailuresByIdRequest : DataHubCLIRequest<GetSyncFailuresResponse>
{
    public GetSyncFailuresByIdRequest()
    {
        RequestType = nameof(GetSyncFailuresByIdRequest);
    }

    public string Select { get; set; }
    public List<string> Ids { get; set; } = new();
}