using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ResolveExternalEntityReferences;

public class ResolveExternalEntityReferencesRequest : IRequest<ResolveExternalEntityReferencesRequestResponse>
{
    public List<JObject> DataHubEntitiesToResolve { get; set; }
    public bool DoNotTrack { get; set; } = false;
}