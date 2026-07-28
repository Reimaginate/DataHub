using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core;

public class MergeEntityResult
{
    public string DataSource { get; set; }
    public string DataHubEntityType { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityId { get; set; }
    public JObject ResultingDataHubEntity { get; set; }
    public JObject ResultingDataHubEntityUpdates { get; set; }
    public JObject ResultingSourceEntity { get; set; }
    public JObject ResultingSourceEntityUpdates { get; set; }
    public string MergeOutcome { get; set; }
    public string FailureReason { get; set; }
}