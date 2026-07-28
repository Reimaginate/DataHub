using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntityByAltKeyResponse
{
    public bool Success { get; set; }
    public JObject Result { get; set; }
    public string FailureReason { get; set; }
}