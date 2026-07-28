using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.CreateDeferredEntityResolutionPromises;

public class CreateDeferredEntityResolutionPromisesResponse
{
    public List<ResolutionPromise> ResultingPromises { get; set; }
}