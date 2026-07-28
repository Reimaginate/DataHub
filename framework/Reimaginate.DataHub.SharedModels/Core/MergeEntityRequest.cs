using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core;

public class MergeEntityRequest
{
    public string DataSource { get; set; }
    public string SourceEntityType { get; set; }
    public string SourceEntityId { get; set; }
    public string DataHubEntityType { get; set; }
    public JObject Data { get; set; }
}