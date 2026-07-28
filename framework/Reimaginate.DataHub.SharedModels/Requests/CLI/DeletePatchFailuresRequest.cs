using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeletePatchFailuresRequest : DataHubCLIRequest<DeletePatchFailuresResponse>
{
    public DeletePatchFailuresRequest()
    {
        RequestType = nameof(DeletePatchFailuresRequest);
    }

    public List<string> PatchFailureIds { get; set; }
}