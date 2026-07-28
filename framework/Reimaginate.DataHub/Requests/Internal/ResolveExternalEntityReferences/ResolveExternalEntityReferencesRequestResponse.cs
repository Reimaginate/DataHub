using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.ResolveExternalEntityReferences;

public class ResolveExternalEntityReferencesRequestResponse
{
    public List<ResolvedEntityReference> ResolvedEntityReferences { get; set; } = new();
    public List<JObject> UpdatedDataHubEntities { get; set; } = new();
}
