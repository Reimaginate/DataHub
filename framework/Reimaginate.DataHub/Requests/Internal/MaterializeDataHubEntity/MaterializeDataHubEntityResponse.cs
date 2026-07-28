using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;

public class MaterializeDataHubEntityResponse
{
    public JObject ResultingEntity { get; set; }
}