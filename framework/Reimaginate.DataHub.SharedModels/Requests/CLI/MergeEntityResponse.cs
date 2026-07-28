using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class MergeEntityResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public string DataSource { get; set; }
    public string DataHubEntityType { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityId { get; set; }
    public JObject ResultingEntity { get; set; }
    public JObject ResultingEntityUpdates { get; set; }
    public string MergeOutcome { get; set; }
}