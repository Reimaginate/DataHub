using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }

    public List<JObject> Results { get; set; }
    public string ContinuationToken { get; set; }
    public int ResultCount { get; set; }
    public bool MoreResultsAvailable { get; set; }
}