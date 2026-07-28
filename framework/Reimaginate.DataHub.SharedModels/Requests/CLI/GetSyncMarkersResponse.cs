using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Markers;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetSyncMarkersResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<SyncMarker> Results { get; set; }
}