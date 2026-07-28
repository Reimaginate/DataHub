using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class PatchDataHubEntitiesWhereResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<PatchEntityResponse> Results { get; set; }
    public string ContinuationToken { get; set; }
    public int ResultCount { get; set; }
    public bool MoreResultsAvailable { get; set; }
    public bool Silent { get; set; }
}