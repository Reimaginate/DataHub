namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class DeleteLogEntriesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
}