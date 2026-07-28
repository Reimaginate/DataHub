namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class UpdateUntrackedEntityResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }

}