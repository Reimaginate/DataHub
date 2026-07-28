using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.GetMaterializedEntitiesById;

public class GetMaterializedEntitiesByIdResponse
{
    public List<JObject> Results { get; set; } = new();
}
