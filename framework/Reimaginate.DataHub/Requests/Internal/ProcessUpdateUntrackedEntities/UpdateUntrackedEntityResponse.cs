namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateUntrackedEntities;

public class ProcessUpdateUntrackedEntityResponse
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public bool Success { get; set; }
    public string FailureReason { get; set; }
}