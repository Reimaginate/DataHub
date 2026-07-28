using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class PatchDataHubEntitiesWhereResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<Client.PatchEntityResponse> Results { get; set; }
    public string ContinuationToken { get; set; }
    public int ResultCount { get; set; }
    public bool MoreResultsAvailable { get; set; }
}