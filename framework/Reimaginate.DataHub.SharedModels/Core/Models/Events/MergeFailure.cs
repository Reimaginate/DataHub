namespace Reimaginate.DataHub.SharedModels.Core.Models.Events;

public class MergeFailure : SyncEvent
{
    public string FailureType { get; set; }
    public string FailureReason { get; set; }
}