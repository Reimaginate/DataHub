using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteTrackingEntriesRequest : DataHubCLIRequest<DeleteTrackingEntriesResponse>
{
    public DeleteTrackingEntriesRequest()
    {
        RequestType = nameof(DeleteTrackingEntriesRequest);
    }

    public List<string> TrackingEntryIds { get; set; }
}