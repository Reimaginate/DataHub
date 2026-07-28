namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class SyncEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public string JobId { get; set; }
}