using Reimaginate.DataHub.SharedModels.Markers;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetSyncMarkerResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public SyncMarker SyncMarker { get; set; }
}