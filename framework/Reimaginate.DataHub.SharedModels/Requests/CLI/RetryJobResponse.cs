namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RetryJobResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
}