namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class SplitDataHubEntityAlternateKeyResponse
{
    public bool Success { get; set; }
    public bool Changed { get; set; }
    public string EntityType { get; set; }
    public string OriginalEntityId { get; set; }
    public string NewEntityId { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public int CopiedTrackingEntries { get; set; }
    public string FailureReason { get; set; }
}
