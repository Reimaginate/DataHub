using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetPatchFailuresByIdRequest : DataHubCLIRequest<GetPatchFailuresResponse>
{
    public GetPatchFailuresByIdRequest()
    {
        RequestType = nameof(GetPatchFailuresByIdRequest);
    }

    public string Select { get; set; }
    public List<string> Ids { get; set; } = new();
}