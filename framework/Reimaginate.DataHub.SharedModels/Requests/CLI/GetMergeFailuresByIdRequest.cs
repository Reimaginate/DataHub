using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetMergeFailuresByIdRequest : DataHubCLIRequest<GetMergeFailuresResponse>
{
    public GetMergeFailuresByIdRequest()
    {
        RequestType = nameof(GetMergeFailuresByIdRequest);
    }

    public string Select { get; set; }
    public List<string> Ids { get; set; } = new();
}