using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.UpdateTrackedEntity;

public class UpdateTrackedEntityResponse
{
    public JObject EntityCurrentState { get; set; }
    public JObject Updates { get; set; }
}