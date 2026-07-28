using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceResolutionPromises;

public class ResolveEntityReferenceResolutionPromisesResponse
{
    public List<JObject> UpdatedDataHubEntities { get; set; }
}