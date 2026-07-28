using System.Collections.Generic;

namespace Reimaginate.DataHub.Requests.Internal.DeleteTrackingData;

public class DeleteTrackingDataResponse
{
    public bool Success { get; set; }
    public List<DeleteTrackingDataFailure> Failures { get; set; } = new();
}