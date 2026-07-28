using Reimaginate.DataHub.SharedModels.Markers;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetMergeMarkerResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public MergeMarker MergeMarker { get; set; }
}