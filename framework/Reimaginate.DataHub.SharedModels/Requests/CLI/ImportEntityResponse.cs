namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class ImportEntityResponse
{
    public bool Success { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string FailureReason { get; set; }
}