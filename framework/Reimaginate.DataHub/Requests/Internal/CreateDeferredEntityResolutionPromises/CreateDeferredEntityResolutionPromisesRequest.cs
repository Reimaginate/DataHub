using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.CreateDeferredEntityResolutionPromises;

public class CreateDeferredEntityResolutionPromisesRequest : IRequest<CreateDeferredEntityResolutionPromisesResponse>
{
    public List<JObject> Entities { get; set; }
}