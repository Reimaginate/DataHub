using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRebaseDataHubEntities;

public class ProcessRebaseDataHubEntitiesResponse
{
    public List<RebaseEntityTrackingResult> Results { get; set; } = new();
}