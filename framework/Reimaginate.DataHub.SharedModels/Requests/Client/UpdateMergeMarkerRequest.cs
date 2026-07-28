using Reimaginate.DataHub.SharedModels.Markers;
using System;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateMergeMarkerRequest : DataHubClientRequest<UpdateMergeMarkerResponse>
{
    public UpdateMergeMarkerRequest()
    {
        RequestType = nameof(UpdateMergeMarkerRequest);
    }
    public MergeMarker MergeMarker { get; set; }
    public string NewValue { get; set; }
    public DateTimeOffset? RunTime { get; set; }
}