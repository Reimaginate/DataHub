using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteMergeFailuresRequest : DataHubCLIRequest<DeleteMergeFailuresResponse>
{
    public DeleteMergeFailuresRequest()
    {
        RequestType = nameof(DeleteMergeFailuresRequest);
    }

    public List<string> MergeFailureIds { get; set; }
}