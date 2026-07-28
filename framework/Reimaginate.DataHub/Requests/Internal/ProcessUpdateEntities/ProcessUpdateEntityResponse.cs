using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.ProcessUpdateEntities;

public class ProcessUpdateEntityResponse
{
    public string DataSource { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public JObject ResultingEntity { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; }
}