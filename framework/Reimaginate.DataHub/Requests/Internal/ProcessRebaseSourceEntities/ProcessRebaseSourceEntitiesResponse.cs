using Reimaginate.DataHub.SharedModels.Core;
using System.Collections.Generic;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseSourceEntities;

public class ProcessRebaseSourceEntitiesResponse
{
    public List<RebaseEntityTrackingResult> Results { get; set; } = new();
}