using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.ProcessNewEntityIntoExistingEntity;

public class ProcessNewEntityIntoExistingEntityResponse
{
    public JObject ResultingEntity { get; set; }
    public JObject EntityUpdates { get; set; }
}