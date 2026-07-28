using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.Client;

public class GetTrackedEntityResult
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public JObject Data { get; set; }
}