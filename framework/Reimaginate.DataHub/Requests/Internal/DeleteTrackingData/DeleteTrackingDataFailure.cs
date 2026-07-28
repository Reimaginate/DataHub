using System;

namespace Reimaginate.DataHub.Requests.Internal.DeleteTrackingData;

public class DeleteTrackingDataFailure
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public Exception Exception { get; set; }
}