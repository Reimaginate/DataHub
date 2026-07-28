using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class GetEntitiesResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<JObject> Results { get; set; } = new();

    public int ResultCount { get; set; }
    
    public string ContinuationToken { get; set; }

    public bool MoreResultsAvailable { get; set; }
}
