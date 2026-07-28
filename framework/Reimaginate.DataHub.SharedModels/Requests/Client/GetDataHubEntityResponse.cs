using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetDataHubEntityResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public JObject Entity { get; set; }

}