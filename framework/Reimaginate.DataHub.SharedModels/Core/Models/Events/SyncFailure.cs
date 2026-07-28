namespace Reimaginate.DataHub.SharedModels.Core.Models.Events;

public class SyncFailure : SyncEvent
{
    public string FailureType { get; set; }
    public string FailureReason { get; set; }
}