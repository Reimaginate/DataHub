using System;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Markers;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class PatchSyncMarkerRequest : DataHubCLIRequest<PatchSyncMarkerResponse>
{
    public PatchSyncMarkerRequest()
    {
        RequestType = nameof(PatchSyncMarkerRequest);
    }

    public string MarkerId { get; set; }
    public string Value { get; set; }
    public bool UpdateValue { get; set; }
    public DateTimeOffset? LastRunTime { get; set; }
    public bool UpdateLastRunTime { get; set; }
    public bool DryRun { get; set; }
}

public class PatchSyncMarkerResponse
{
    public bool Success { get; set; }
    public string MarkerId { get; set; }
    public bool Changed { get; set; }
    public string Status { get; set; }
    public string Reason { get; set; }
    public SyncMarker Result { get; set; }
}
