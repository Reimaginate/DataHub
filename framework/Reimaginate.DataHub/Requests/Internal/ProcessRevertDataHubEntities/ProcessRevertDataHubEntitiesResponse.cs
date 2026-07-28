using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Requests.Internal.ProcessRevertDataHubEntities;

public class ProcessRevertDataHubEntitiesResponse
{
    public List<RevertDataHubEntityResult> Results { get; set; } = new();
}
