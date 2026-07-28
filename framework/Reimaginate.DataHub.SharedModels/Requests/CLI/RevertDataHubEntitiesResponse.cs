using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class RevertDataHubEntitiesResponse
{
    public List<RevertDataHubEntityResult> Results { get; set; } = new();
}
