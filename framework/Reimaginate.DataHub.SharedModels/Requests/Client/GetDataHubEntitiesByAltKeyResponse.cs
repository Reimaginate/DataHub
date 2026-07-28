using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntitiesByAltKeyResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<JObject> Results { get; set; }
}