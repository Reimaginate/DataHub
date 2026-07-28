using System;
using Reimaginate.DataHub.SharedModels.Markers;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateSyncMarkerRequest : DataHubClientRequest<UpdateSyncMarkerResponse>
{
    public UpdateSyncMarkerRequest()
    {
        RequestType = nameof(UpdateSyncMarkerRequest);
    }
    public SyncMarker SyncMarker { get; set; }
    public string NewValue { get; set; }
    public DateTimeOffset? RunTime { get; set; }
}